using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class BulkEmailService(ApplicationDbContext db, HtmlSanitizationService sanitizer)
{
    public async Task<BulkEmail> CreateAsync(string subject, string body, string senderId)
    {
        var recipients = await db.Users
            .Where(u => u.WantsEmailNotifications)
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

        db.BulkEmails.Add(bulkEmail);
        await db.SaveChangesAsync();
        return bulkEmail;
    }

    public async Task<BulkEmail?> GetByIdAsync(int id)
    {
        return await db.BulkEmails
            .Include(b => b.Sender)
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
}
