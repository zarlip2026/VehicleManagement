using System.ComponentModel.DataAnnotations;

namespace VehicleManagement.Models
{
    public class Vehicle : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(Constants.OwnerNameMaxLength)]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(Constants.ManufacturerNameMaxLength)]
        public string Manufacturer { get; set; } = string.Empty;

        public int YearOfManufacture { get; set; }

        public static int MaximumManufactureYear => DateTime.Today.Year;
        public static int MinimumManufactureYear => Constants.MinimumManufactureYear;
        public static string ManufactureYearError =>
            $"Year of manufacture must be between {MinimumManufactureYear} and {MaximumManufactureYear} (future years are not allowed).";

        [Range((double)Constants.MinimumWeightKg, (double)Constants.MaximumWeightKg)]
        public decimal WeightKg { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            ValidateNumbers(YearOfManufacture, WeightKg);

        public static IEnumerable<ValidationResult> ValidateNumbers(int? year, decimal? weight)
        {
            if (year.HasValue && (year.Value < MinimumManufactureYear || year.Value > MaximumManufactureYear))
                yield return new ValidationResult(ManufactureYearError, new[] { nameof(YearOfManufacture) });
            if (weight.HasValue && decimal.Round(weight.Value, Constants.WeightDecimalPlaces) != weight.Value)
                yield return new ValidationResult($"Weight must have at most {Constants.WeightDecimalPlaces} decimal places.", new[] { nameof(WeightKg) });
        }

        // Category is determined dynamically by the CategoryService based on WeightKg
    }
}


