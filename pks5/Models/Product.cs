using System.ComponentModel.DataAnnotations;

namespace pks5.Models;

public sealed class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? SpecificationsJson { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(0, int.MaxValue)]
    public int MinimalStock { get; set; }

    [Range(1, int.MaxValue)]
    public int ProductionTimePerUnit { get; set; }

    public List<ProductMaterial> ProductMaterials { get; set; } = [];
}

