using System.ComponentModel.DataAnnotations;

namespace pks5.Models;

public sealed class ProductionLine
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = LineStatus.Active;

    [Range(0.5, 2.0)]
    public decimal EfficiencyFactor { get; set; } = 1.0m;

    public int? CurrentWorkOrderId { get; set; }

    public List<WorkOrder> WorkOrders { get; set; } = [];
}

public static class LineStatus
{
    public const string Active = "Active";
    public const string Stopped = "Stopped";
}

