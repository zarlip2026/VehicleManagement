using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// Configuration - enable MVC with views
builder.Services.AddControllersWithViews();

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
