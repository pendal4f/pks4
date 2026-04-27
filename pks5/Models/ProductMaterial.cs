using System.ComponentModel.DataAnnotations;

namespace pks5.Models;

public sealed class ProductMaterial
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    [Range(0, double.MaxValue)]
    public decimal QuantityNeeded { get; set; }
}

