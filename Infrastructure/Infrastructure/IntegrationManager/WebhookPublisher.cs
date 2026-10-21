using System.Threading.Channels;
using Application.Common.Services.IntegrationManager;

namespace Infrastructure.IntegrationManager;

/// <summary>Marks code that runs on behalf of a workflow rule, so its own changes do not raise events again.</summary>
public static class WorkflowGuard
{
    private static readonly AsyncLocal<bool> Flag = new();
    public static bool Active { get => Flag.Value; set => Flag.Value = value; }
}

/// <summary>In-memory queues between the code that raises events and the dispatchers that react to them.</summary>
public class WebhookPublisher : IWebhookPublisher
{
    private static Channel<WebhookEvent> NewChannel() => Channel.CreateBounded<WebhookEvent>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true
    });

    private readonly Channel<WebhookEvent> _webhooks = NewChannel();
    private readonly Channel<WebhookEvent> _workflows = NewChannel();

    public ChannelReader<WebhookEvent> Reader => _webhooks.Reader;
    public ChannelReader<WebhookEvent> WorkflowReader => _workflows.Reader;

    public void Publish(WebhookEvent webhookEvent)
    {
        _webhooks.Writer.TryWrite(webhookEvent);
        if (!webhookEvent.Name.StartsWith("test.", StringComparison.Ordinal))
        {
            _workflows.Writer.TryWrite(webhookEvent);
        }
    }
}
