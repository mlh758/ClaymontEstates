using Server.Data;

namespace Server.Services;

public class AuditService(ApplicationDbContext db)
{
    public async Task LogAsync(AuditAction action, string actorEmail, string? ipAddress, Dictionary<string, string>? details = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Action = action,
            ActorEmail = actorEmail,
            IpAddress = ipAddress,
            Details = details ?? []
        });
        await db.SaveChangesAsync();
    }
}
