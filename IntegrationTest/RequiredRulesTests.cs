using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VehicleManagement.Data;
using VehicleManagement.Models;
using Xunit;
using static VehicleManagement.IntegrationTests.FormTestHelper;

namespace VehicleManagement.IntegrationTests;

public class RequiredRulesTests
{
    [Fact]
    public async Task CreateCategory_RequiresIcon_ThenAcceptsUploadedIcon()
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await Token(client, "/CategoriesManage/Create");
        using var missing = await client.PostAsync("/CategoriesManage/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Name"] = "Light", ["MinWeightKg"] = "0.01", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Contains("A category icon is required.", await missing.Content.ReadAsStringAsync());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        Assert.Empty(await db.Categories.ToListAsync());
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Light"), "Name");
        form.Add(new StringContent("0.01"), "MinWeightKg");
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=")), "icon", "icon.png");
        using var created = await client.PostAsync("/CategoriesManage/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.NotEmpty((await db.Categories.SingleAsync()).Icon!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VehicleSave_WithMissingOrInvalidCategories_ShowsError(bool invalid)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        if (invalid) db.Categories.Add(new Category { Name = "Invalid", MinWeightKg = 500m, Icon = new byte[] { 1 } });
        var vehicle = new Vehicle { OwnerName = "Original", Manufacturer = "Toyota", YearOfManufacture = 2020, WeightKg = 500m };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        foreach (var path in new[] { "/VehiclesManage/Create", $"/VehiclesManage/Edit/{vehicle.Id}" })
        {
            var token = await Token(client, path);
            using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = vehicle.Id.ToString(), ["OwnerName"] = "Changed", ["Manufacturer"] = "Toyota",
                ["YearOfManufacture"] = "2020", ["WeightKg"] = "500", ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Configure valid weight categories", await response.Content.ReadAsStringAsync());
        }
        db.ChangeTracker.Clear();
        Assert.Equal("Original", Assert.Single(await db.Vehicles.ToListAsync()).OwnerName);
    }

    private sealed class DatabaseFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled) throw new DbUpdateException("PRIVATE_DATABASE_DETAIL");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task DatabaseFailure_ReturnsFriendly500WithoutExceptionDetails()
    {
        var failure = new DatabaseFailure();
        var database = Guid.NewGuid().ToString();
        using var original = new TestApp();
        using var app = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<VehicleDbContext>();
            services.RemoveAll<DbContextOptions<VehicleDbContext>>();
            services.AddDbContext<VehicleDbContext>(options => options.UseInMemoryDatabase(database).AddInterceptors(failure));
        }));
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        failure.Enabled = true;
        var token = await Token(client, "/VehiclesManage/Create");
        using var response = await client.PostAsync("/VehiclesManage/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["OwnerName"] = "Amy", ["Manufacturer"] = "Toyota", ["YearOfManufacture"] = "2020",
            ["WeightKg"] = "500", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("We could not complete your request", html);
        Assert.DoesNotContain("PRIVATE_DATABASE_DETAIL", html);
        Assert.DoesNotContain("DbUpdateException", html);
        Assert.Empty(await db.Vehicles.ToListAsync());
    }
}
