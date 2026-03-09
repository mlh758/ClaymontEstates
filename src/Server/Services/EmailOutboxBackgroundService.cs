using FluentEmail.Core;
using Polly;
using Polly.Registry;

namespace Server.Services;

public class EmailOutboxBackgroundService(
    EmailOutboxService outbox,
    IServiceScopeFactory scopeFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<EmailOutboxBackgroundService> logger) : BackgroundService
{
    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline("email-smtp");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in outbox.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _pipeline.ExecuteAsync(async ct =>
                {
                    using var scope = scopeFactory.CreateScope();
                    var emailFactory = scope.ServiceProvider.GetRequiredService<IFluentEmailFactory>();

                    var email = emailFactory.Create()
                        .To(message.To)
                        .Subject(message.Subject)
                        .Body(message.Body, isHtml: true);

                    if (message.ReplyTo is not null)
                        email.ReplyTo(message.ReplyTo);

                    var response = await email.SendAsync(ct);
                    if (!response.Successful)
                        throw new InvalidOperationException(
                            $"Email send failed: {string.Join(", ", response.ErrorMessages)}");
                }, stoppingToken);

                logger.LogInformation("Sent email to {To}: {Subject}", message.To, message.Subject);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send email to {To} after retries: {Subject}",
                    message.To, message.Subject);
            }
        }
    }
}
