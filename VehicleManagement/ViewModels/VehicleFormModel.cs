using System.ComponentModel.DataAnnotations;
using VehicleManagement.Models;

namespace VehicleManagement.ViewModels;

public class VehicleFormModel : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [MaxLength(Constants.OwnerNameMaxLength)]
    public string OwnerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(Constants.ManufacturerNameMaxLength)]
    public string Manufacturer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year of manufacture is required.")]
    public int? YearOfManufacture { get; set; }

    [Required(ErrorMessage = "Weight is required.")]
    [Range((double)Constants.MinimumWeightKg, (double)Constants.MaximumWeightKg)]
    public decimal? WeightKg { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        Vehicle.ValidateNumbers(YearOfManufacture, WeightKg);

    public Vehicle ToVehicle() => new()
    {
        Id = Id,
        OwnerName = OwnerName,
        Manufacturer = Manufacturer,
        YearOfManufacture = YearOfManufacture ?? throw new InvalidOperationException("Validate the vehicle form before saving."),
        WeightKg = WeightKg ?? throw new InvalidOperationException("Validate the vehicle form before saving.")
    };

    public static VehicleFormModel FromVehicle(Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        OwnerName = vehicle.OwnerName,
        Manufacturer = vehicle.Manufacturer,
        YearOfManufacture = vehicle.YearOfManufacture,
        WeightKg = vehicle.WeightKg
    };
}
