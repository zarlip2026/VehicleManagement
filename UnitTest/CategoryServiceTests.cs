using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;
using VehicleManagement.Services;
using Xunit;

namespace VehicleManagement.UnitTests
{
    public class CategoryServiceTests
    {
        [Fact]
        public async Task MissingOrEmptyIcon_IsRejectedWithoutSaving()
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            foreach (var icon in new byte[]?[] { null, Array.Empty<byte>() })
            {
                var error = await Assert.ThrowsAsync<CategoryValidationException>(() => service.AddAsync(
                    new Category { Name = "Light", MinWeightKg = 0.01m, Icon = icon }));
                Assert.Equal("A category icon is required.", error.Message);
            }
            Assert.Empty(await db.Categories.ToListAsync());
        }

        private VehicleDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<VehicleDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new VehicleDbContext(options);
        }

        [Fact]
        public async Task Boundaries_EachValidWeightResolvesToExactlyOneCategory()
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m });
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Heavy", MinWeightKg = 2500m });
            var cases = new (decimal weight, string expected)[]
            {
                (0.01m, "Light"), (499.99m, "Light"), (500m, "Medium"),
                (500.01m, "Medium"), (2499.99m, "Medium"), (2500m, "Heavy"),
                (2500.01m, "Heavy"), (1000000m, "Heavy")
            };
            
            var categories = (await service.ListAsync()).ToList();
            
            foreach (var (weight, expected) in cases)
            {
                var matches = categories.Where((c, i) => c.MinWeightKg <= weight &&
                    (i == categories.Count - 1 || weight < categories[i + 1].MinWeightKg)).ToList();
                Assert.Equal(expected, Assert.Single(matches).Name);
                Assert.Equal(expected, (await service.GetCategoryForWeightAsync(weight))?.Name);
            }
        }

        [Fact]
        public async Task DuplicateThreshold_EditRejectedWithoutChangingConfiguration()
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m });
            
            var heavy = await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Heavy", MinWeightKg = 2500m });
            
            await Assert.ThrowsAsync<CategoryValidationException>(() => service.UpdateAsync(
                new Category { Icon = new byte[] { 1 }, Id = heavy.Id, Name = "Heavy", MinWeightKg = 500m }));
            
            db.ChangeTracker.Clear();
            
            Assert.Equal(2500m, (await service.GetByIdAsync(heavy.Id))!.MinWeightKg);
            await service.ValidateConfigurationAsync();
        }

        [Fact]
        public async Task ChangingAndDeletingMiddleThreshold_LeavesNoGap()
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            
            var medium = await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m });
            
            await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Heavy", MinWeightKg = 2500m });
            
            await service.UpdateAsync(new Category { Icon = new byte[] { 1 }, Id = medium.Id, Name = "Medium", MinWeightKg = 600m });
            Assert.Equal("Light", (await service.GetCategoryForWeightAsync(550m))!.Name);
            Assert.Equal("Medium", (await service.GetCategoryForWeightAsync(600m))!.Name);
            
            await service.DeleteAsync(medium.Id);
            Assert.Equal("Light", (await service.GetCategoryForWeightAsync(600m))!.Name);
            Assert.Equal("Light", (await service.GetCategoryForWeightAsync(2499.99m))!.Name);
            Assert.Equal("Heavy", (await service.GetCategoryForWeightAsync(2500m))!.Name);
            
            await service.ValidateConfigurationAsync();
        }

        [Fact]
        public async Task LegacyZeroBoundary_FailedCreateAndDeleteLeaveDatabaseUnchanged()
        {
            using var db = CreateContext();
            var legacy = new Category { Icon = new byte[] { 1 }, Name = "Legacy", MinWeightKg = 0m };
            
            db.Categories.Add(legacy);
            await db.SaveChangesAsync();
            
            var service = new CategoryService(db);
            await Assert.ThrowsAsync<CategoryValidationException>(() => service.AddAsync(
                new Category { Icon = new byte[] { 1 }, Name = "New", MinWeightKg = 0.01m }));
            
            await db.SaveChangesAsync();
            Assert.Single(await db.Categories.ToListAsync());

            var second = new Category { Icon = new byte[] { 1 }, Name = "Second", MinWeightKg = 0.01m };
            
            db.Categories.Add(second);
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<CategoryValidationException>(() => service.DeleteAsync(second.Id));
            await db.SaveChangesAsync();
            
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Categories.CountAsync());
            Assert.NotNull(await db.Categories.FindAsync(second.Id));
        }

        [Fact]
        public async Task LegacyZeroBoundary_CanBeCorrectedByEditingFirstCategory()
        {
            using var db = CreateContext();
            var legacy = new Category { Icon = new byte[] { 1 }, Name = "Legacy", MinWeightKg = 0m };
            
            db.Categories.Add(legacy);
            
            await db.SaveChangesAsync();
            
            var service = new CategoryService(db);
            
            await service.UpdateAsync(new Category { Icon = new byte[] { 1 }, Id = legacy.Id, Name = "Legacy", MinWeightKg = 0.01m });
            await service.ValidateConfigurationAsync();
        }

        [Fact]
        public async Task FirstCategory_StartsAtPointZeroOne_AndDeletePromotesNext()
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            
            var first = await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            Assert.Equal(first.Id, (await service.GetCategoryForWeightAsync(0.01m))!.Id);
            
            var next = await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m });
            await service.DeleteAsync(first.Id);
            
            db.ChangeTracker.Clear();
            Assert.Equal(0.01m, (await service.GetByIdAsync(next.Id))!.MinWeightKg);
            
            await service.ValidateConfigurationAsync();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("0.001")]
        [InlineData("0.011")]
        public async Task InvalidMinimum_CreateAndEditRejectBeforeSaving(string value)
        {
            using var db = CreateContext();
            var service = new CategoryService(db);
            
            var weight = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            await Assert.ThrowsAsync<CategoryValidationException>(() => service.AddAsync(
                new Category { Icon = new byte[] { 1 }, Name = "Invalid", MinWeightKg = weight }));
            Assert.Empty(await db.Categories.ToListAsync());
            
            var first = await service.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            await Assert.ThrowsAsync<CategoryValidationException>(() => service.UpdateAsync(
                new Category { Icon = new byte[] { 1 }, Id = first.Id, Name = "Invalid", MinWeightKg = weight }));
            
            db.ChangeTracker.Clear();
            
            Assert.Equal(0.01m, (await service.GetByIdAsync(first.Id))!.MinWeightKg);
        }

        [Fact]
        public async Task Delete_LastCategory_RejectsAndPreservesCategory()
        {
            using var db = CreateContext();
            var category = new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m };
            
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            var error = await Assert.ThrowsAsync<CategoryValidationException>(() => new CategoryService(db).DeleteAsync(category.Id));
            Assert.Equal("Cannot delete the last remaining category.", error.Message);
            
            db.ChangeTracker.Clear();
            Assert.Single(await db.Categories.ToListAsync());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Update_ChangesWeightAndKeepsOrReplacesIcon(bool replaceIcon)
        {
            using var db = CreateContext();
            
            var original = new byte[] { 1, 2, 3 };
            var replacement = new byte[] { 4, 5, 6 };
            var category = new Category { Name = "Medium", MinWeightKg = 500m, Icon = original };
            
            db.Categories.AddRange(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m }, category);
            
            await db.SaveChangesAsync();
            await new CategoryService(db).UpdateAsync(new Category
            {
                Id = category.Id, Name = category.Name, MinWeightKg = 600m,
                Icon = replaceIcon ? replacement : null
            });
            
            db.ChangeTracker.Clear();
            
            var saved = await db.Categories.FindAsync(category.Id);
            
            Assert.Equal(600m, saved!.MinWeightKg);
            Assert.Equal(replaceIcon ? replacement : original, saved.Icon);
        }

        [Fact]
        public async Task Update_LastCategoryHigherWeight_RejectsBeforeSaving()
        {
            using var db = CreateContext();
            var category = new Category { Name = "Light", MinWeightKg = 0.01m, Icon = new byte[] { 1 } };
            
            db.Categories.Add(category);

            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<CategoryValidationException>(() => new CategoryService(db).UpdateAsync(
                new Category { Icon = new byte[] { 1 }, Id = category.Id, Name = "Changed", MinWeightKg = 100m }));
            
            db.ChangeTracker.Clear();
            var saved = await db.Categories.FindAsync(category.Id);
            
            Assert.Equal(0.01m, saved!.MinWeightKg);
            Assert.Equal("Light", saved.Name);
            Assert.Equal(new byte[] { 1 }, saved.Icon);
        }

        [Fact]
        public async Task GetById_ReturnsCategoryOrNullWhenMissing()
        {
            using var db = CreateContext();
            var category = new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m };
            db.Categories.Add(category);
            
            await db.SaveChangesAsync();
            var svc = new CategoryService(db);

            Assert.Equal("Light", (await svc.GetByIdAsync(category.Id))?.Name);
            Assert.Null(await svc.GetByIdAsync(category.Id + 1));
        }

        [Fact]
        public async Task UpdateIcon_PersistsIconAndPreservesCategoryDetails()
        {
            using var db = CreateContext();
            var category = new Category { Name = "Medium", MinWeightKg = 500m, Icon = new byte[] { 1 } };
            db.Categories.Add(category);
            
            await db.SaveChangesAsync();
            
            var svc = new CategoryService(db);
            var icon = new byte[] { 2, 3, 4 };

            Assert.True(await svc.UpdateIconAsync(category.Id, icon));
            db.ChangeTracker.Clear();
            
            var saved = await db.Categories.FindAsync(category.Id);
            Assert.NotNull(saved);
            Assert.Equal(icon, saved.Icon);
            Assert.Equal("Medium", saved.Name);
            Assert.Equal(500m, saved.MinWeightKg);
        }

        [Fact]
        public async Task UpdateIcon_MissingCategory_ReturnsFalseWithoutCreatingCategory()
        {
            using var db = CreateContext();
            var svc = new CategoryService(db);

            Assert.False(await svc.UpdateIconAsync(123, new byte[] { 1 }));
            Assert.Empty(await db.Categories.ToListAsync());
        }

        [Fact]
        public async Task ValidateConfiguration_FirstCategoryMustBePointZeroOne()
        {
            using var db = CreateContext();
            db.Categories.Add(new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m });
            await db.SaveChangesAsync();

            var svc = new CategoryService(db);
            await Assert.ThrowsAsync<CategoryValidationException>(() => svc.ValidateConfigurationAsync());
        }

        [Fact]
        public async Task GetCategoryForWeight_Boundary_Inclusive()
        {
            using var db = CreateContext();
            db.Categories.AddRange(
                new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m },
                new Category { Icon = new byte[] { 1 }, Name = "Medium", MinWeightKg = 500m },
                new Category { Icon = new byte[] { 1 }, Name = "Heavy", MinWeightKg = 2500m }
            );
            await db.SaveChangesAsync();

            var svc = new CategoryService(db);
            var c1 = await svc.GetCategoryForWeightAsync(500m);
            Assert.Equal("Medium", c1?.Name);

            var c2 = await svc.GetCategoryForWeightAsync(499.99m);
            Assert.Equal("Light", c2?.Name);
        }

        [Fact]
        public async Task AddingDuplicateMinWeight_Throws()
        {
            using var db = CreateContext();
            db.Categories.Add(new Category { Icon = new byte[] { 1 }, Name = "Light", MinWeightKg = 0.01m });
            await db.SaveChangesAsync();

            var svc = new CategoryService(db);
            await Assert.ThrowsAsync<CategoryValidationException>(() => svc.AddAsync(new Category { Icon = new byte[] { 1 }, Name = "Duplicate", MinWeightKg = 0.01m }));
        }
    }
}


