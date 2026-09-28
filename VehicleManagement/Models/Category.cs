using System.ComponentModel.DataAnnotations;

namespace VehicleManagement.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(Constants.CategoryNameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range((double)Constants.MinimumWeightKg, (double)Constants.MaximumWeightKg, ErrorMessage = "Min Weight must be between {1} and {2} kg.")]
        public decimal MinWeightKg { get; set; }

        // Icon stored as binary image data (varbinary(max)). Will be uploaded from UI.
        public byte[]? Icon { get; set; }

        // MinWeightKg is inclusive. Ordering by MinWeightKg ascending determines ranges.
    }
}


