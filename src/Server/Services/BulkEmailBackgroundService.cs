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
        var bulkEmailService = scope.ServiceProvider.GetRequiredService<BulkEmailService>();

        var retryCutoff = DateTime.UtcNow.AddMinutes(-30);

        // Find bulk emails that have deliverable recipients
        // Exclude recipients that already sent, or failed within the last 30 minutes
        var pendingEmails = await db.BulkEmails
            .Include(b => b.Sender)
            .Include(b => b.Attachments)
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

                        // Stream each attachment from disk; dispose the handles once the
                        // message has been sent (or this attempt has failed).
                        var attachmentStreams = new List<Stream>();
                        try
                        {
                            foreach (var attachment in bulkEmail.Attachments)
                            {
                                var path = bulkEmailService.GetPhysicalPath(attachment);
                                if (!File.Exists(path))
                                {
                                    logger.LogWarning(
                                        "Attachment file missing for bulk email {BulkEmailId}: {Path}",
                                        bulkEmail.Id, path);
                                    continue;
                                }

                                var fileStream = File.OpenRead(path);
                                attachmentStreams.Add(fileStream);
                                email.Attach(new FluentEmail.Core.Models.Attachment
                                {
                                    Data = fileStream,
                                    Filename = attachment.FileName,
                                    ContentType = attachment.ContentType
                                });
                            }

                            var response = await email.SendAsync(token);
                            if (!response.Successful)
                                throw new InvalidOperationException(
                                    $"Email send failed: {string.Join(", ", response.ErrorMessages)}");
                        }
                        finally
                        {
                            foreach (var stream in attachmentStreams)
                                await stream.DisposeAsync();
                        }
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
                // Delivery is finished — purge the attachment files now to reclaim disk.
                // The attachment metadata rows are kept so the email's history still shows
                // how many files were attached and their sizes.
                bulkEmailService.DeleteAttachmentFiles(bulkEmail.Attachments);
                bulkEmail.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        // Clean up completed bulk emails older than a week. Their attachment files were
        // already purged when delivery completed; the cascade delete removes the metadata rows.
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
