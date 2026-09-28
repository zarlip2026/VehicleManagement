using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VehicleManagement.Data;
using VehicleManagement.Models;
using Xunit;

using static VehicleManagement.IntegrationTests.FormTestHelper;

namespace VehicleManagement.IntegrationTests;

public class VehicleManagementTest
{
    [Fact]
    public async Task CreateAndEdit_LoadManufacturersFromDatabase()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        Assert.Equal(new[] { "Ferrari", "Honda", "Mazda", "Mercedes", "Toyota" },
            await db.Manufacturers.OrderBy(m => m.Name).Select(m => m.Name).ToArrayAsync());
        db.Manufacturers.Add(new Manufacturer { Name = "New manufacturer" });
        var vehicle = NewVehicle();
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        foreach (var path in new[] { "/VehiclesManage/Create", $"/VehiclesManage/Edit/{vehicle.Id}" })
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains("<option value=\"New manufacturer\">New manufacturer</option>", html);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1879, false)]
    [InlineData(0, true)]
    [InlineData(1880, true)]
    public async Task ManufactureYear_CreateAndEditEnforceDynamicBoundaries(int yearCase, bool valid)
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        var vehicle = NewVehicle();
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var year = yearCase <= 1 ? DateTime.Today.Year + yearCase : yearCase;
        foreach (var path in new[] { "/VehiclesManage/Create", $"/VehiclesManage/Edit/{vehicle.Id}" })
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains($"data-val-range-min=\"1880\"", html);
            Assert.Contains($"data-val-range-max=\"{DateTime.Today.Year}\"", html);
            var form = Form(path.EndsWith("Create") ? 0 : vehicle.Id);
            form["YearOfManufacture"] = year.ToString();
            using var response = await Post(client, path, form);
            Assert.Equal(valid ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.StatusCode);
            if (!valid) Assert.Contains(Vehicle.ManufactureYearError, await response.Content.ReadAsStringAsync());
        }
        db.ChangeTracker.Clear();
        var saved = await db.Vehicles.ToListAsync();
        Assert.Equal(valid ? 2 : 1, saved.Count);
        Assert.All(saved, v => Assert.Equal(valid ? year : 2020, v.YearOfManufacture));
    }

    [Fact]
    public async Task VehiclesIndex_ShowsCurrentCategoryIconWithoutWeightColumn()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();

        var medium = new Category { Name = "Medium", MinWeightKg = 500m, Icon = new byte[] { 4, 5, 6 } };
        
        db.Categories.AddRange(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1, 2, 3 } }, medium);
        db.Vehicles.Add(new Vehicle { OwnerName = "Owner", Manufacturer = "Toyota", YearOfManufacture = 2020, WeightKg = 500m });
        
        await db.SaveChangesAsync();
        
        var html = await client.GetStringAsync("/VehiclesManage");
        Assert.DoesNotContain("<th>Weight", html);
        Assert.DoesNotContain("<td>500", html);
        Assert.Contains("data:image/png;base64,BAUG", html);
        
        medium.MinWeightKg = 600m;
        
        await db.SaveChangesAsync();
        
        html = await client.GetStringAsync("/VehiclesManage");
        Assert.Contains("data:image/png;base64,AQID", html);
        Assert.DoesNotContain("data:image/png;base64,BAUG", html);
    }


    private static Vehicle NewVehicle(string owner = "Amy", string manufacturer = "Toyota", int year = 2020, decimal weight = 500m) =>
        new() { OwnerName = owner, Manufacturer = manufacturer, YearOfManufacture = year, WeightKg = weight };

    private static Dictionary<string, string> Form(int id = 0) => new()
    {
        ["Id"] = id.ToString(), ["OwnerName"] = "Amy", ["Manufacturer"] = "Toyota",
        ["YearOfManufacture"] = "2020", ["WeightKg"] = "500.00"
    };

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> form, string? tokenPath = null)
    {
        form["__RequestVerificationToken"] = await Token(client, tokenPath ?? path);
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task CreateEditDelete_PersistsChangesAndRedirectsToIndex()
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        
        using var created = await Post(client, "/VehiclesManage/Create", Form());
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal("/VehiclesManage", created.Headers.Location?.OriginalString);
        
        var vehicle = Assert.Single(await db.Vehicles.AsNoTracking().ToListAsync());
        Assert.Equal("Amy", vehicle.OwnerName);
        Assert.Equal(500m, vehicle.WeightKg);
        
        var edited = Form(vehicle.Id);
        edited["OwnerName"] = "Zoe";
        edited["Manufacturer"] = "Honda";
        edited["YearOfManufacture"] = "2023";
        edited["WeightKg"] = "2500.01";
        
        using var updated = await Post(client, $"/VehiclesManage/Edit/{vehicle.Id}", edited);
        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        
        vehicle = await db.Vehicles.AsNoTracking().SingleAsync();
        Assert.Equal("Zoe", vehicle.OwnerName);
        Assert.Equal("Honda", vehicle.Manufacturer);
        Assert.Equal(2023, vehicle.YearOfManufacture);
        Assert.Equal(2500.01m, vehicle.WeightKg);
        
        using var deleted = await Post(client, $"/VehiclesManage/Delete/{vehicle.Id}", Form(vehicle.Id));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Empty(await db.Vehicles.ToListAsync());
    }

    [Theory]
    [InlineData("OwnerName", "")]
    [InlineData("Manufacturer", "")]
    [InlineData("YearOfManufacture", "0")]
    [InlineData("YearOfManufacture", "3001")]
    [InlineData("WeightKg", "0")]
    [InlineData("WeightKg", "-1")]
    [InlineData("WeightKg", "1000000.01")]
    [InlineData("WeightKg", "500.001")]
    [InlineData("WeightKg", "not-a-number")]
    public async Task InvalidCreateAndEdit_RenderFieldErrorAndPreserveDatabase(string field, string value)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var vehicle = NewVehicle();
        db.Vehicles.Add(vehicle);
        
        await db.SaveChangesAsync();
        
        foreach (var path in new[] { "/VehiclesManage/Create", $"/VehiclesManage/Edit/{vehicle.Id}" })
        {
            var form = Form(vehicle.Id);
            form[field] = value;
            using var response = await Post(client, path, form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Matches($"class=\"[^\"]*field-validation-error[^\"]*\"[^>]*data-valmsg-for=\"{field}\"", html);
            Assert.Contains("Toyota", html); // Manufacturer options survive validation failures.
        }
        db.ChangeTracker.Clear();
        
        var saved = Assert.Single(await db.Vehicles.ToListAsync());
        Assert.Equal("Amy", saved.OwnerName);
        Assert.Equal("Toyota", saved.Manufacturer);
        Assert.Equal(2020, saved.YearOfManufacture);
        Assert.Equal(500m, saved.WeightKg);
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task MissingVehicle_GetAndPostReturnNotFound(string action)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        var path = $"/VehiclesManage/{action}/999";
        
        using var get = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        
        using var post = await Post(client, path, Form(999), "/VehiclesManage/Create");
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Edit_MismatchedIdReturnsBadRequestWithoutChangingVehicle()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var vehicle = NewVehicle();
        
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        
        using var response = await Post(client, $"/VehiclesManage/Edit/{vehicle.Id}", Form(vehicle.Id + 1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        db.ChangeTracker.Clear();
        Assert.Equal("Amy", (await db.Vehicles.SingleAsync()).OwnerName);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task PostWithoutAntiforgeryToken_IsRejected(string action)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        
        var vehicle = NewVehicle();
        db.Vehicles.Add(vehicle);
        
        await db.SaveChangesAsync();
        
        using var response = await client.PostAsync($"/VehiclesManage/{action}/{vehicle.Id}", new FormUrlEncodedContent(Form(vehicle.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        db.ChangeTracker.Clear();
        Assert.Equal("Amy", Assert.Single(await db.Vehicles.ToListAsync()).OwnerName);
    }

    [Theory]
    [InlineData("ownername", false, "Amy")]
    [InlineData("ownername", true, "Zoe")]
    [InlineData("manufacturer", false, "Zoe")]
    [InlineData("manufacturer", true, "Amy")]
    [InlineData("yearofmanufacture", false, "Zoe")]
    [InlineData("yearofmanufacture", true, "Amy")]
    [InlineData("weight", false, "Amy")]
    [InlineData("weight", true, "Zoe")]
    public async Task Index_SortsRenderedRows(string sortBy, bool descending, string firstOwner)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Vehicles.AddRange(NewVehicle("Amy", "Toyota", 2020, 100m), NewVehicle("Zoe", "Honda", 2010, 900m));
        
        await db.SaveChangesAsync();
        
        var html = await client.GetStringAsync($"/VehiclesManage?sortBy={sortBy}&desc={descending}");
        
        var owners = Regex.Matches(html, @"<tr>\s*<td>\s*(Amy|Zoe)\s*</td>").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(2, owners.Length);
        Assert.Equal(firstOwner, owners[0]);
    }
}



