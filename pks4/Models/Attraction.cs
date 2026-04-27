using System.ComponentModel.DataAnnotations;

namespace pks4.Models;

public sealed class Attraction
{
    public int Id { get; set; }

    public int CityId { get; set; }

    public City City { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ShortDescription { get; set; }

    public string? History { get; set; }

    public string? PhotoPath { get; set; }

    public string? OpeningHours { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? VisitCost { get; set; }
}
