using System.ComponentModel.DataAnnotations;
using VehicleManagement.Models;
using Xunit;

namespace VehicleManagement.UnitTests;

public class VehicleValidationTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1881, true)]
    [InlineData(1880, true)]
    [InlineData(1879, false)]
    public void ManufactureYear_OnlyAllows1880ThroughCurrentYear(int yearCase, bool expected)
    {
        var vehicle = new Vehicle
        {
            OwnerName = "Amy", Manufacturer = "Toyota", WeightKg = 500m,
            YearOfManufacture = yearCase <= 1 ? DateTime.Today.Year + yearCase : yearCase
        };
        var errors = new List<ValidationResult>();
        Assert.Equal(expected, Validator.TryValidateObject(vehicle, new ValidationContext(vehicle), errors, true));
        if (!expected)
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(Vehicle.YearOfManufacture)));
    }

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("499.99", true)]
    [InlineData("500", true)]
    [InlineData("500.00", true)]
    [InlineData("500.01", true)]
    [InlineData("1000000", true)]
    [InlineData("0", false)]
    [InlineData("-0.01", false)]
    [InlineData("0.001", false)]
    [InlineData("500.001", false)]
    [InlineData("1000000.01", false)]
    public void Weight_RequiresPositiveValueWithAtMostTwoDecimalPlaces(string weight, bool expected)
    {
        var vehicle = new Vehicle
        {
            OwnerName = "Amy", Manufacturer = "Toyota", YearOfManufacture = 2020,
            WeightKg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture)
        };
        var errors = new List<ValidationResult>();

        Assert.Equal(expected, Validator.TryValidateObject(vehicle, new ValidationContext(vehicle), errors, true));
        
        if (!expected) 
            Assert.Contains(errors, e => e.MemberNames.Contains(nameof(Vehicle.WeightKg)));
    }
}

