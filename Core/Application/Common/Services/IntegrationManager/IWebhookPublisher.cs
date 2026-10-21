namespace Application.Common.Services.IntegrationManager;

public record WebhookEvent(string Name, object Data, string? TargetEndpointId = null);

/// <summary>Queues an event for delivery to subscribed webhook endpoints. Never blocks and never throws.</summary>
public interface IWebhookPublisher
{
    void Publish(WebhookEvent webhookEvent);
}
