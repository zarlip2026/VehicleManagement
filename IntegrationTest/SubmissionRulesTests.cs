using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;
using Xunit;
using static VehicleManagement.IntegrationTests.FormTestHelper;

namespace VehicleManagement.IntegrationTests;

public class SubmissionRulesTests
{
    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    public async Task UnlistedManufacturer_ShowsValidationAndDoesNotSave(string action)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        var original = new Vehicle { OwnerName = "Original", Manufacturer = "Toyota", YearOfManufacture = 2020, WeightKg = 100m };
        db.Vehicles.Add(original);
        await db.SaveChangesAsync();
        var path = action == "Create" ? "/VehiclesManage/Create" : $"/VehiclesManage/Edit/{original.Id}";
        var token = await Token(client, path);
        using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = original.Id.ToString(), ["OwnerName"] = "Changed", ["Manufacturer"] = "Not in the list",
            ["YearOfManufacture"] = "2021", ["WeightKg"] = "200", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Select a manufacturer from the available list.", html);
        Assert.Contains("Toyota", html);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Vehicles.ToListAsync());
        Assert.Equal("Original", saved.OwnerName);
        Assert.Equal("Toyota", saved.Manufacturer);
    }

    [Theory]
    [InlineData("89504E470D0A1A0A", "image/png")]
    [InlineData("FFD8FFE0", "image/jpeg")]
    [InlineData("474946383961", "image/gif")]
    [InlineData("424D00000000", "image/bmp")]
    [InlineData("524946460000000057454250", "image/webp")]
    [InlineData("00000010667479706176696600000000", "image/avif")]
    public async Task Icons_RenderCorrectMediaTypeOnEveryPage(string hex, string mediaType)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var bytes = Convert.FromHexString(hex);
        var category = new Category { Name = "Light", MinWeightKg = 0.01m, Icon = bytes };
        db.Categories.Add(category);
        db.Vehicles.Add(new Vehicle { OwnerName = "Amy", Manufacturer = "Toyota", YearOfManufacture = 2020, WeightKg = 100m });
        await db.SaveChangesAsync();
        foreach (var path in new[] { "/CategoriesManage", $"/CategoriesManage/Edit/{category.Id}", "/VehiclesManage" })
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains($"data:{mediaType};base64,{Convert.ToBase64String(bytes)}", html);
        }
    }

    [Theory]
    [InlineData("/CategoriesManage/Create")]
    [InlineData("/CategoriesManage/Edit/1")]
    public async Task UploadForms_ShowSupportedExtensionsAndLimit(string path)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Id = 1, Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } });
        await db.SaveChangesAsync();
        var html = await client.GetStringAsync(path);
        Assert.Contains("Supported extensions: .png, .jpg, .jpeg, .webp, .gif, .bmp, .avif.", html);
        Assert.Contains("Maximum file size: 1 MB (1,000,000 bytes).", html);
        Assert.DoesNotContain(".heic", html);
        Assert.DoesNotContain(".heif", html);
    }
}
