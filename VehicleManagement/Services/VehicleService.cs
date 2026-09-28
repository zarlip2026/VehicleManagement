using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Services;

public class VehicleService : IVehicleService
{
    private readonly VehicleDbContext _db;
    private readonly ICategoryService _categories;


    public VehicleService(VehicleDbContext db, ICategoryService categories)
    {
        _db = db;
        _categories = categories;

    }

    public async Task<IEnumerable<Manufacturer>> GetManufacturersAsync() =>
        await _db.Manufacturers.AsNoTracking().OrderBy(m => m.Name).ToListAsync();

    public async Task<IEnumerable<(Vehicle vehicle, Category? category)>> ListAsync(string? sortBy, bool desc = false)
    {
        IQueryable<Vehicle> query = _db.Vehicles.AsNoTracking();

        query = sortBy?.ToLowerInvariant() switch
        {
            "ownername" => desc ? query.OrderByDescending(v => v.OwnerName) : query.OrderBy(v => v.OwnerName),
            "manufacturer" => desc ? query.OrderByDescending(v => v.Manufacturer) : query.OrderBy(v => v.Manufacturer),
            "yearofmanufacture" => desc ? query.OrderByDescending(v => v.YearOfManufacture) : query.OrderBy(v => v.YearOfManufacture),
            "weight" => desc ? query.OrderByDescending(v => v.WeightKg) : query.OrderBy(v => v.WeightKg),
            _ => desc ? query.OrderByDescending(v => v.Id) : query.OrderBy(v => v.Id)
        };

        var vehicles = await query.ToListAsync();

        var categories = (await _categories.ListAsync()).OrderByDescending(c => c.MinWeightKg).ToList();

        return vehicles.Select(v => (v, categories.FirstOrDefault(c => c.MinWeightKg <= v.WeightKg))).ToList();
    }

    public async Task<Vehicle?> GetByIdAsync(int id) => await _db.Vehicles.FindAsync(id);

    public async Task AddAsync(Vehicle vehicle)
    {
        await ValidateCategoryAsync(vehicle.WeightKg);
        vehicle.Manufacturer = await GetValidManufacturerAsync(vehicle.Manufacturer);
        _db.Vehicles.Add(vehicle);

        await _db.SaveChangesAsync();
    }

    public async Task<bool> UpdateAsync(Vehicle vehicle)
    {
        var existing = await _db.Vehicles.FindAsync(vehicle.Id);

        if (existing == null) 
            return false;

        await ValidateCategoryAsync(vehicle.WeightKg);
        var manufacturer = await GetValidManufacturerAsync(vehicle.Manufacturer);

        existing.OwnerName = vehicle.OwnerName;
        existing.Manufacturer = manufacturer;
        existing.YearOfManufacture = vehicle.YearOfManufacture;
        existing.WeightKg = vehicle.WeightKg;

        await _db.SaveChangesAsync();

        return true;
    }

    private async Task<string> GetValidManufacturerAsync(string name)
    {
        var manufacturer = await _db.Manufacturers.AsNoTracking()
            .Where(m => m.Name == name).Select(m => m.Name).FirstOrDefaultAsync();
        return manufacturer ?? throw new VehicleValidationException("Select a manufacturer from the available list.");
    }

    private async Task ValidateCategoryAsync(decimal weightKg)
    {
        try
        {
            await _categories.ValidateConfigurationAsync();

            if (await _categories.GetCategoryForWeightAsync(weightKg) == null)
                throw new CategoryValidationException("No category covers this weight.");
        }
        catch (CategoryValidationException)
        {
            throw new VehicleValidationException("Vehicle could not be saved. Configure valid weight categories starting at 0.01 kg before saving a vehicle.");
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _db.Vehicles.FindAsync(id);

        if (existing == null) 
            return false;

        _db.Vehicles.Remove(existing);

        await _db.SaveChangesAsync();

        return true;
    }
}

