using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VehicleManagement.Data;
using VehicleManagement.Models;
using VehicleManagement.Services;
using Xunit;

namespace VehicleManagement.UnitTests;

public class VehicleServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInvalidCategories_BlockCreateAndEdit(bool invalid)
    {
        using var db = CreateContext();
        if (invalid) db.Categories.Add(new Category { Name = "Invalid", MinWeightKg = 500m, Icon = new byte[] { 1 } });
        var existing = Vehicle("Original", "Toyota", 2020, 100m);
        db.Vehicles.Add(existing);
        await db.SaveChangesAsync();
        var service = Service(db);
        await Assert.ThrowsAsync<VehicleValidationException>(() => service.AddAsync(Vehicle("New", "Honda", 2020, 500m)));
        var update = Vehicle("Changed", "Honda", 2020, 500m);
        update.Id = existing.Id;
        await Assert.ThrowsAsync<VehicleValidationException>(() => service.UpdateAsync(update));
        db.ChangeTracker.Clear();
        Assert.Equal("Original", Assert.Single(await db.Vehicles.ToListAsync()).OwnerName);
    }

    private static VehicleDbContext CreateContext()
    {
        var db = new VehicleDbContext(new DbContextOptionsBuilder<VehicleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }
    private static VehicleService Service(VehicleDbContext db) => new(db, new CategoryService(db));
    private static Vehicle Vehicle(string owner, string manufacturer, int year, decimal weight) =>
        new() { OwnerName = owner, Manufacturer = manufacturer, YearOfManufacture = year, WeightKg = weight };

    [Theory]
    [InlineData("ownername", false)]
    [InlineData("ownername", true)]
    [InlineData("manufacturer", false)]
    [InlineData("manufacturer", true)]
    [InlineData("yearofmanufacture", false)]
    [InlineData("yearofmanufacture", true)]
    [InlineData("weight", false)]
    [InlineData("weight", true)]
    public async Task List_SortsBothDirections(string sort, bool descending)
    {
        using var db = CreateContext();
        var a = Vehicle("Amy", "Toyota", 2020, 100m);
        var b = Vehicle("Zoe", "Honda", 2010, 900m);
        db.Vehicles.AddRange(a, b);
        
        await db.SaveChangesAsync();
        
        var result = (await Service(db).ListAsync(sort, descending)).Select(x => x.vehicle.Id);
        var ascending = sort is "manufacturer" or "yearofmanufacture" ? new[] { b.Id, a.Id } : new[] { a.Id, b.Id };
        Assert.Equal(descending ? ascending.Reverse() : ascending, result);
    }

    [Fact]
    public async Task List_UsesCurrentInclusiveCategoryBoundaries()
    {
        using var db = CreateContext();
        var medium = new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m };
        db.Categories.AddRange(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m }, medium);
        db.Vehicles.AddRange(Vehicle("Amy", "Toyota", 2020, 499.99m), Vehicle("Bob", "Honda", 2021, 500m));
        
        await db.SaveChangesAsync();
        
        var svc = Service(db);

        Assert.Equal(new[] { "Light", "Medium" }, (await svc.ListAsync(null)).Select(x => x.category?.Name));
        
        medium.MinWeightKg = 600m;
        await db.SaveChangesAsync();
        
        Assert.All(await svc.ListAsync(null), x => Assert.Equal("Light", x.category?.Name));
    }

    [Fact]
    public async Task Crud_PersistsChangesAndHandlesMissingVehicles()
    {
        using var db = CreateContext();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        var svc = Service(db);
        var vehicle = Vehicle("Amy", "Toyota", 2020, 100m);
        
        await svc.AddAsync(vehicle);
        
        db.ChangeTracker.Clear();
        Assert.Equal("Amy", (await svc.GetByIdAsync(vehicle.Id))?.OwnerName);
        
        var edited = Vehicle("Bob", "Honda", 2021, 500m);
        edited.Id = vehicle.Id;
        Assert.True(await svc.UpdateAsync(edited));
        
        db.ChangeTracker.Clear();

        var saved = await svc.GetByIdAsync(vehicle.Id);
        Assert.NotNull(saved);

        Assert.Equal("Bob", saved.OwnerName);
        Assert.Equal("Honda", saved.Manufacturer);
        Assert.Equal(2021, saved.YearOfManufacture);
        Assert.Equal(500m, saved.WeightKg);

        Assert.True(await svc.DeleteAsync(vehicle.Id));

        Assert.Null(await svc.GetByIdAsync(vehicle.Id));

        Assert.False(await svc.UpdateAsync(edited));
        Assert.False(await svc.DeleteAsync(vehicle.Id));
    }

    [Fact]
    public async Task Manufacturers_ComeFromDatabase()
    {
        using var db = CreateContext();
        db.Manufacturers.Add(new Manufacturer { Name = "Custom manufacturer" });
        await db.SaveChangesAsync();
        Assert.Contains(await Service(db).GetManufacturersAsync(), m => m.Name == "Custom manufacturer");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unlisted")]
    [InlineData("Toyota'; DROP TABLE Vehicles;--")]
    public async Task UnknownManufacturer_CreateAndEditRejectWithoutChangingData(string manufacturer)
    {
        using var db = CreateContext();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        var original = Vehicle("Original", "Toyota", 2020, 100m);
        db.Vehicles.Add(original);
        await db.SaveChangesAsync();
        var service = Service(db);
        var invalid = Vehicle("Changed", manufacturer, 2021, 200m);
        var error = await Assert.ThrowsAsync<VehicleValidationException>(() => service.AddAsync(invalid));
        Assert.Equal("Select a manufacturer from the available list.", error.Message);
        invalid.Id = original.Id;
        await Assert.ThrowsAsync<VehicleValidationException>(() => service.UpdateAsync(invalid));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Vehicles.ToListAsync());
        Assert.Equal("Original", saved.OwnerName);
        Assert.Equal("Toyota", saved.Manufacturer);
    }

    [Fact]
    public async Task NewManufacturerFromDatabase_IsAcceptedWithoutCodeChanges()
    {
        using var db = CreateContext();
        db.Manufacturers.Add(new Manufacturer { Name = "New manufacturer" });
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        await Service(db).AddAsync(Vehicle("Amy", "New manufacturer", 2020, 100m));
        Assert.Equal("New manufacturer", (await db.Vehicles.SingleAsync()).Manufacturer);
    }
}




