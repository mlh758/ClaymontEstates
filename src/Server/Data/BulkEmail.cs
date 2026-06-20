using System.ComponentModel.DataAnnotations;

namespace Server.Data;

public class BulkEmail
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(10000)]
    public string Body { get; set; } = string.Empty;

    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<EmailRecipient> Recipients { get; set; } = [];

    public ICollection<BulkEmailAttachment> Attachments { get; set; } = [];
}

public class BulkEmailAttachment
{
    public int Id { get; set; }

    public int BulkEmailId { get; set; }
    public BulkEmail BulkEmail { get; set; } = null!;

    /// <summary>Original file name shown to the user and used as the email attachment name.</summary>
    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Randomized name of the file as stored on disk.</summary>
    [Required]
    [MaxLength(500)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}

public class EmailRecipient
{
    public int Id { get; set; }

    public int BulkEmailId { get; set; }
    public BulkEmail BulkEmail { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime? AttemptedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? FailedAt { get; set; }
}
