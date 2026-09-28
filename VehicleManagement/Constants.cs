namespace VehicleManagement;

/// <summary>Fixed business limits shared by validation, persistence and forms.</summary>
public static class Constants
{
    public const int MinimumManufactureYear = 1880;
    public const decimal MinimumWeightKg = 0.01m;
    public const decimal MaximumWeightKg = 1_000_000m;
    public const decimal WeightInputStepKg = MinimumWeightKg;
    public const int WeightDecimalPlaces = 2;
    public const int WeightDatabasePrecision = 18;
    public const int OwnerNameMaxLength = 200;
    public const int ManufacturerNameMaxLength = 200;
    public const int CategoryNameMaxLength = 100;
}
