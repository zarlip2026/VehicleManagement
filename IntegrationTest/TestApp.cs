using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VehicleManagement.Data;
using VehicleManagement.Models;
using Xunit;

namespace VehicleManagement.IntegrationTests;

internal sealed class TestApp : WebApplicationFactory<Program>
    {
        protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<VehicleDbContext>().Database.EnsureCreated();
            return host;
        }

        private readonly string _database = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Satisfy startup configuration; all database operations still use the isolated in-memory provider below.
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=VehicleManagement_TestOnly;Integrated Security=True;");
            // Test hosts must not depend on the user's Event Log or persisted encryption keys.
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<VehicleDbContext>();
                services.RemoveAll<DbContextOptions<VehicleDbContext>>();
                services.AddDbContext<VehicleDbContext>(options => options.UseInMemoryDatabase(_database));
            });
        }
    }


internal static class FormTestHelper
{
    internal static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token must be rendered in the form.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

}

