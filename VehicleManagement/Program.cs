using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddErrorFile(builder.Configuration, builder.Environment.ContentRootPath);

// Configuration - enable MVC with views
builder.Services.AddControllersWithViews(options => options.Filters.Add<ValidationLoggingFilter>());

// DbContext placeholder - user must set ConnectionStrings:DefaultConnection in appsettings.json
builder.Services.AddDbContext<VehicleDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn);
    }
});

// Register services
builder.Services.AddScoped<VehicleManagement.Services.ICategoryService, VehicleManagement.Services.CategoryService>();
builder.Services.AddScoped<VehicleManagement.Services.IVehicleService, VehicleManagement.Services.VehicleService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    using var scope = app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["RequestId"] = context.TraceIdentifier
    });
    await next(context);
});

app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();
app.UseRouting();

app.UseHttpsRedirection();
app.UseAuthorization();

// Default route for MVC controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Expose the implicit Program class for integration tests (WebApplicationFactory<Program>)
public partial class Program { }
