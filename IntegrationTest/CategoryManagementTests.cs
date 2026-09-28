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

public class CategoryManagementTests
{
    [Fact]
    public async Task LegacyZeroBoundary_CreateAndDeleteShowErrorWithoutSavingOr404()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Icon = new byte[] { 1 }, Name = "Legacy", MinWeightKg = 0m });
        await db.SaveChangesAsync();

        var token = await Token(client, "/CategoriesManage/Create");
        using var create = await client.PostAsync("/CategoriesManage/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "New", ["MinWeightKg"] = "0.01", ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Contains("An existing category still starts at 0 kg.", await create.Content.ReadAsStringAsync());
        Assert.Single(await db.Categories.ToListAsync());

        var second = new Category { Icon = new byte[] { 1 }, Name = "Second", MinWeightKg = 0.01m };

        db.Categories.Add(second);
        await db.SaveChangesAsync();

        var path = $"/CategoriesManage/Delete/{second.Id}";
        token = await Token(client, path);
        using var delete = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = second.Id.ToString(), ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Contains("An existing category still starts at 0 kg.", await delete.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();

        Assert.Equal(2, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task ZeroMinimum_CreateAndEditDisplayPositiveWeightError()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var category = new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m };
        
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        foreach (var path in new[] { "/CategoriesManage/Create", $"/CategoriesManage/Edit/{category.Id}" })
        {
            var token = await Token(client, path);
            using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = category.Id.ToString(), ["Name"] = "Invalid", ["MinWeightKg"] = "0.0",
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Min Weight must be between 0.01 and 1000000 kg.", await response.Content.ReadAsStringAsync());
        }
        db.ChangeTracker.Clear();
        Assert.Equal(0.01m, Assert.Single(await db.Categories.ToListAsync()).MinWeightKg);
    }

    [Fact]
    public async Task CreateDuplicateWeight_DisplaysErrorAndDoesNotSave()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        db.Categories.Add(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });

        await db.SaveChangesAsync();

        var token = await Token(client, "/CategoriesManage/Create");
        using var response = await client.PostAsync("/CategoriesManage/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Duplicate", ["MinWeightKg"] = "0.01", ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("MinWeight must be unique.", await response.Content.ReadAsStringAsync());
        Assert.Single(await db.Categories.ToListAsync());
    }

    [Fact]
    public async Task DeleteLastCategory_RendersReasonAndPreservesRecord()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var category = new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m };
        db.Categories.Add(category);

        await db.SaveChangesAsync();

        var token = await Token(client, $"/CategoriesManage/Delete/{category.Id}");

        using var response = await client.PostAsync($"/CategoriesManage/Delete/{category.Id}", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["Id"] = category.Id.ToString(), ["__RequestVerificationToken"] = token }));
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cannot delete the last remaining category.", await response.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.Categories.ToListAsync());
    }

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
    [InlineData("600", true)]
    [InlineData("0.01", false)]
    public async Task EditWithoutUpload_PreservesIconAndDisplaysValidation(string weight, bool valid)
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var icon = new byte[] { 1, 2, 3 };
        var category = new Category { Name = "Medium", MinWeightKg = 500m, Icon = icon };

        db.Categories.AddRange(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m }, category);
        await db.SaveChangesAsync();

        var token = await Token(client, $"/CategoriesManage/Edit/{category.Id}");
        using var form = new MultipartFormDataContent();

        form.Add(new StringContent(category.Id.ToString()), "Id");
        form.Add(new StringContent("Medium"), "Name");
        form.Add(new StringContent(weight), "MinWeightKg");
        form.Add(new StringContent(token), "__RequestVerificationToken");

        using var response = await client.PostAsync($"/CategoriesManage/Edit/{category.Id}", form);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data:image/png;base64,AQID", html);

        if (!valid) 
            Assert.Contains("MinWeight must be unique.", html);

        db.ChangeTracker.Clear();

        var saved = await db.Categories.FindAsync(category.Id);
        Assert.Equal(valid ? 600m : 500m, saved!.MinWeightKg);
        Assert.Equal(icon, saved.Icon);
    }

}



