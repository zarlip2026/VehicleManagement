namespace VehicleManagement.Services;

public class VehicleValidationException : Exception
{
    public VehicleValidationException(string message) : base(message) { }
}
