using System.Collections.Generic;
using System.Threading.Tasks;
using VehicleManagement.Models;

namespace VehicleManagement.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> ListAsync();
        Task<Category?> GetByIdAsync(int id);
        Task<bool> UpdateIconAsync(int id, byte[] icon);
        Task<Category> AddAsync(Category category);
        Task<Category> UpdateAsync(Category category);
        Task DeleteAsync(int id);
        Task<Category?> GetCategoryForWeightAsync(decimal weightKg);
        Task ValidateConfigurationAsync();
    }
}
