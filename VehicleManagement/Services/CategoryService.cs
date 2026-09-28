using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Services
{
    public class CategoryValidationException : Exception
    {
        public CategoryValidationException(string message) : base(message) { }
    }

    public class CategoryService : ICategoryService
    {
        private readonly VehicleDbContext _db;

        public CategoryService(VehicleDbContext db)
        {
            _db = db;
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _db.Categories.FindAsync(id);
        }

        public async Task<Category> AddAsync(Category category)
        {
            ValidateMinWeight(category.MinWeightKg);

            await EnsureMinWeightUnique(category.MinWeightKg);

            var weights = await _db.Categories.Select(c => c.MinWeightKg).ToListAsync();

            ValidateWeights(weights.Append(category.MinWeightKg));

            ValidateIcon(category.Icon);

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return category;
        }

        public async Task<Category> UpdateAsync(Category category)
        {
            ValidateMinWeight(category.MinWeightKg);

            var existing = await _db.Categories.FindAsync(category.Id);
            if (existing == null) throw new KeyNotFoundException("Category not found");

            if (existing.MinWeightKg != category.MinWeightKg)
            {
                await EnsureMinWeightUnique(category.MinWeightKg, category.Id);
            }

            var weights = await _db.Categories.Where(c => c.Id != category.Id).Select(c => c.MinWeightKg).ToListAsync();
            
            ValidateWeights(weights.Append(category.MinWeightKg));

            ValidateIcon(category.Icon is { Length: > 0 } ? category.Icon : existing.Icon);

            existing.Name = category.Name;
            // An omitted upload means keep the stored icon.
            if (category.Icon is { Length: > 0 }) 
                existing.Icon = category.Icon;

            existing.MinWeightKg = category.MinWeightKg;

            await _db.SaveChangesAsync();

            return existing;
        }

        public async Task DeleteAsync(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return;

            var total = await _db.Categories.CountAsync();
            if (total <= 1)
            {
                // Do not allow deleting last category
                throw new CategoryValidationException("Cannot delete the last remaining category.");
            }

            var remaining = await _db.Categories.Where(c => c.Id != id).OrderBy(c => c.MinWeightKg).ToListAsync();

            bool isFirst = cat.MinWeightKg < remaining[0].MinWeightKg;

            var proposedWeights = remaining.Select((c, index) => isFirst && index == 0 ? Constants.MinimumWeightKg : c.MinWeightKg);

            ValidateWeights(proposedWeights);

            _db.Categories.Remove(cat);
            if (isFirst) remaining[0].MinWeightKg = Constants.MinimumWeightKg;

            await _db.SaveChangesAsync();
        }

        public async Task<bool> UpdateIconAsync(int id, byte[] icon)
        {
            ValidateIcon(icon);
            var category = await _db.Categories.FindAsync(id);

            if (category == null) return false;

            category.Icon = icon;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<Category?> GetCategoryForWeightAsync(decimal weightKg)
        {
            // Pick the category with the largest MinWeightKg <= weightKg
            return await _db.Categories
                .Where(c => c.MinWeightKg <= weightKg)
                .OrderByDescending(c => c.MinWeightKg)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Category>> ListAsync()
        {
            return await _db.Categories.OrderBy(c => c.MinWeightKg).ToListAsync();
        }

        private static void ValidateIcon(byte[]? icon)
        {
            if (icon is not { Length: > 0 })
                throw new CategoryValidationException("A category icon is required.");
        }

        private static void ValidateMinWeight(decimal weight)
        {
            if (weight < Constants.MinimumWeightKg || weight > Constants.MaximumWeightKg)
                throw new CategoryValidationException($"Min Weight must be between {Constants.MinimumWeightKg} and {Constants.MaximumWeightKg} kg.");

            if (decimal.Round(weight, Constants.WeightDecimalPlaces) != weight)
                throw new CategoryValidationException($"Min Weight must have at most {Constants.WeightDecimalPlaces} decimal places.");
        }

        private async Task EnsureMinWeightUnique(decimal minWeight, int? ignoreId = null)
        {
            var q = _db.Categories.Where(c => c.MinWeightKg == minWeight);

            if (ignoreId.HasValue) 
                q = q.Where(c => c.Id != ignoreId.Value);

            if (await q.AnyAsync()) 
                throw new CategoryValidationException("MinWeight must be unique.");
        }

        public async Task ValidateConfigurationAsync()
        {
            ValidateWeights(await _db.Categories.Select(c => c.MinWeightKg).ToListAsync());
        }

        private static void ValidateWeights(IEnumerable<decimal> weights)
        {
            var ordered = weights.OrderBy(weight => weight).ToList();

            if (ordered.Count == 0)
                throw new CategoryValidationException("At least one category required.");
            
            if (ordered[0] == 0m)
                throw new CategoryValidationException($"An existing category still starts at 0 kg. Edit its minimum to {Constants.MinimumWeightKg} kg before adding or changing other categories.");
            
            if (ordered[0] != Constants.MinimumWeightKg)
                throw new CategoryValidationException($"The first category must have MinWeightKg = {Constants.MinimumWeightKg}.");
            
            foreach (var weight in ordered) 
                ValidateMinWeight(weight);

            if (ordered.Distinct().Count() != ordered.Count)
                throw new CategoryValidationException("MinWeight must be unique.");
        }
    }
}

