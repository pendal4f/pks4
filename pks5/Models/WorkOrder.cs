using System.ComponentModel.DataAnnotations;

namespace pks5.Models;

public sealed class WorkOrder
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int? ProductionLineId { get; set; }
    public ProductionLine? ProductionLine { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EstimatedEndDate { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = WorkOrderStatus.Pending;

    [Range(0, 100)]
    public decimal ProgressPercent { get; set; }
}

public static class WorkOrderStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

