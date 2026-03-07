using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Data;

public enum AuditAction
{
    CreateUser,
    EditUser,
    SendPasswordReset,
    UploadDocument,
    DeleteDocument
}

public class AuditEvent
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public AuditAction Action { get; set; }

    [Required]
    [MaxLength(256)]
    public string ActorEmail { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Required]
    [Column(TypeName = "TEXT")]
    public string DetailsJson { get; set; } = "{}";

    [NotMapped]
    public Dictionary<string, string> Details
    {
        get => JsonSerializer.Deserialize<Dictionary<string, string>>(DetailsJson) ?? [];
        set => DetailsJson = JsonSerializer.Serialize(value);
    }
}
