using System.Threading.Channels;

namespace Server.Services;

public record EmailMessage(string To, string Subject, string Body, string? ReplyTo = null);

public class EmailOutboxService
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public async ValueTask QueueAsync(EmailMessage message, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(message, ct);
    }
}
