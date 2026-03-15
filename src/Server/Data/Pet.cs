using System.ComponentModel.DataAnnotations;

namespace Server.Data;

public class Pet
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal? Weight { get; set; }

    [Required]
    [MaxLength(50)]
    public string Species { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
