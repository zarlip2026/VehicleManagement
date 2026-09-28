using VehicleManagement.Models;

namespace VehicleManagement.Services;

public interface IVehicleService
{
    Task<IEnumerable<(Vehicle vehicle, Category? category)>> ListAsync(string? sortBy, bool desc = false);
    Task<Vehicle?> GetByIdAsync(int id);
    Task<IEnumerable<Manufacturer>> GetManufacturersAsync();
    Task AddAsync(Vehicle vehicle);
    Task<bool> UpdateAsync(Vehicle vehicle);
    Task<bool> DeleteAsync(int id);
}

