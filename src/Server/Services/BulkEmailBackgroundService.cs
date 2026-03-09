using FluentEmail.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;
using Server.Data;

namespace Server.Services;

public class BulkEmailBackgroundService(
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<BulkEmailBackgroundService> logger) : BackgroundService
{
    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline("email-smtp");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing bulk emails");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessEmailsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailFactory = scope.ServiceProvider.GetRequiredService<IFluentEmailFactory>();

        var retryCutoff = DateTime.UtcNow.AddMinutes(-30);

        // Find bulk emails that have deliverable recipients
        // Exclude recipients that already sent, or failed within the last 30 minutes
        var pendingEmails = await db.BulkEmails
            .Include(b => b.Sender)
            .Include(b => b.Recipients
                .Where(r => r.SentAt == null && (r.FailedAt == null || r.FailedAt < retryCutoff)))
                .ThenInclude(r => r.User)
            .Where(b => b.CompletedAt == null)
            .ToListAsync(ct);

        foreach (var bulkEmail in pendingEmails)
        {
            if (bulkEmail.StartedAt is null)
            {
                bulkEmail.StartedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            foreach (var recipient in bulkEmail.Recipients)
            {
                if (recipient.User.Email is null) continue;

                recipient.AttemptedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                try
                {
                    await _pipeline.ExecuteAsync(async token =>
                    {
                        var email = emailFactory.Create()
                            .To(recipient.User.Email)
                            .Subject(bulkEmail.Subject)
                            .Body(bulkEmail.Body, isHtml: true);

                        if (bulkEmail.Sender?.Email is not null)
                        {
                            email.ReplyTo(bulkEmail.Sender.Email);
                        }

                        var response = await email.SendAsync(token);
                        if (!response.Successful)
                            throw new InvalidOperationException(
                                $"Email send failed: {string.Join(", ", response.ErrorMessages)}");
                    }, ct);

                    recipient.SentAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    recipient.FailedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    logger.LogError(ex, "Failed to send bulk email {BulkEmailId} to {Email} after retries",
                        bulkEmail.Id, recipient.User.Email);
                }
            }

            // Check if all recipients have been sent or are permanently stuck
            // A bulk email is complete when no unsent recipients remain
            var allDone = !await db.EmailRecipients
                .AnyAsync(r => r.BulkEmailId == bulkEmail.Id && r.SentAt == null, ct);

            if (allDone)
            {
                bulkEmail.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        // Clean up completed bulk emails older than a week
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var staleEmails = await db.BulkEmails
            .Where(b => b.CompletedAt != null && b.CompletedAt < cutoff)
            .ToListAsync(ct);

        if (staleEmails.Count > 0)
        {
            db.BulkEmails.RemoveRange(staleEmails);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Cleaned up {Count} completed bulk emails older than 7 days", staleEmails.Count);
        }
    }
}
