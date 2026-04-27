using System.ComponentModel.DataAnnotations;

namespace pks4.Models;

public sealed class City
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Region { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Population { get; set; }

    public string? History { get; set; }

    public string? CoatOfArmsImagePath { get; set; }

    public string? PhotoPath { get; set; }

    public List<Attraction> Attractions { get; set; } = [];
}
