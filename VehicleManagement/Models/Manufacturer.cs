using System.ComponentModel.DataAnnotations;

namespace VehicleManagement.Models
{
    public class Manufacturer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(Constants.ManufacturerNameMaxLength)]
        public string Name { get; set; } = string.Empty;
    }
}

