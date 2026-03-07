using System.ComponentModel.DataAnnotations;

namespace Server.Data;

public class Event
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public TimeSpan Duration { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public bool IsPublic { get; set; }

    public bool RequiresRsvp { get; set; }

    [Required]
    [MaxLength(256)]
    public string CreatedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Rsvp> Rsvps { get; set; } = [];
}

public class Rsvp
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}
