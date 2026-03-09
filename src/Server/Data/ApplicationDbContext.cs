using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string,
        IdentityUserClaim<string>, ApplicationUserRole, IdentityUserLogin<string>,
        IdentityRoleClaim<string>, IdentityUserToken<string>>(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<BulkEmail> BulkEmails => Set<BulkEmail>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<Rsvp> Rsvps => Set<Rsvp>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUserRole>(entity =>
        {
            entity.HasOne(ur => ur.Role)
                .WithMany()
                .HasForeignKey(ur => ur.RoleId);

            entity.HasOne<ApplicationUser>()
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.HasIndex(a => a.Timestamp);
            entity.Property(a => a.Action).HasConversion<string>();
        });

        builder.Entity<BulkEmail>(entity =>
        {
            entity.HasOne(b => b.Sender)
                .WithMany()
                .HasForeignKey(b => b.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmailRecipient>(entity =>
        {
            entity.HasOne(r => r.BulkEmail)
                .WithMany(b => b.Recipients)
                .HasForeignKey(r => r.BulkEmailId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmergencyContact>(entity =>
        {
            entity.HasOne(ec => ec.User)
                .WithMany()
                .HasForeignKey(ec => ec.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Rsvp>(entity =>
        {
            entity.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();

            entity.HasOne(r => r.Event)
                .WithMany(e => e.Rsvps)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
