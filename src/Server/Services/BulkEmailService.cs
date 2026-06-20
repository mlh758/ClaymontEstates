using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

/// <summary>An attachment supplied at compose time, before it is persisted to disk.</summary>
public record BulkEmailAttachmentInput(Stream Content, string FileName, string ContentType, long FileSize);

public class BulkEmailService(ApplicationDbContext db, HtmlSanitizationService sanitizer, IConfiguration config)
{
    // Keep the total well under the SES message limit (~40 MB), which includes
    // base64 inflation (~1.37x) plus the message body.
    public const long MaxTotalAttachmentBytes = 20 * 1024 * 1024;

    private string StoragePath => Path.GetFullPath(
        config.GetValue<string>("BulkEmailAttachmentStorage:Path")
        ?? Path.Combine(AppContext.BaseDirectory, "BulkEmailAttachments"));

    public async Task<(bool Success, string? Error, BulkEmail? Email)> CreateAsync(
        string subject, string body, string senderId, IReadOnlyList<BulkEmailAttachmentInput> attachments)
    {
        var totalSize = attachments.Sum(a => a.FileSize);
        if (totalSize > MaxTotalAttachmentBytes)
            return (false, $"Attachments exceed the maximum total size of {MaxTotalAttachmentBytes / 1024 / 1024} MB.", null);

        var recipients = await db.Users
            .Where(u => u.WantsEmailNotifications && u.Email != null)
            .Select(u => u.Id)
            .ToListAsync();

        var bulkEmail = new BulkEmail
        {
            Subject = subject,
            Body = sanitizer.Sanitize(body),
            SenderId = senderId,
            Recipients = recipients.Select(userId => new EmailRecipient
            {
                UserId = userId,
            }).ToList()
        };

        if (attachments.Count > 0)
        {
            var storagePath = StoragePath;
            Directory.CreateDirectory(storagePath);

            foreach (var att in attachments)
            {
                var storedName = $"{Guid.NewGuid()}{Path.GetExtension(att.FileName)}";
                var fullPath = Path.Combine(storagePath, storedName);

                await using (var fileOut = File.Create(fullPath))
                {
                    await att.Content.CopyToAsync(fileOut);
                }

                bulkEmail.Attachments.Add(new BulkEmailAttachment
                {
                    FileName = att.FileName,
                    StoredFileName = storedName,
                    ContentType = string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType,
                    FileSizeBytes = att.FileSize
                });
            }
        }

        db.BulkEmails.Add(bulkEmail);
        await db.SaveChangesAsync();
        return (true, null, bulkEmail);
    }

    public async Task<BulkEmail?> GetByIdAsync(int id)
    {
        return await db.BulkEmails
            .Include(b => b.Sender)
            .Include(b => b.Attachments)
            .Include(b => b.Recipients)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<BulkEmail>> GetAllAsync()
    {
        return await db.BulkEmails
            .Include(b => b.Sender)
            .Include(b => b.Recipients)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Absolute path to an attachment's file on disk.</summary>
    public string GetPhysicalPath(BulkEmailAttachment attachment) =>
        Path.Combine(StoragePath, attachment.StoredFileName);

    /// <summary>Removes the on-disk files for the given attachments. Missing files are ignored.</summary>
    public void DeleteAttachmentFiles(IEnumerable<BulkEmailAttachment> attachments)
    {
        foreach (var att in attachments)
        {
            var path = Path.Combine(StoragePath, att.StoredFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
