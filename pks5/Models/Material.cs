using System.ComponentModel.DataAnnotations;

namespace pks5.Models;

public sealed class Material
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    [StringLength(50)]
    public string UnitOfMeasure { get; set; } = "шт";

    [Range(0, double.MaxValue)]
    public decimal MinimalStock { get; set; }

    public List<ProductMaterial> ProductMaterials { get; set; } = [];
}

