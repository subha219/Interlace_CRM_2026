using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Services.IntegrationManager;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.IntegrationManager;

/// <summary>
/// Delivers queued events to webhook endpoints: signed JSON POST, up to 3 attempts, one log row per delivery.
/// Connections to private, loopback and link-local addresses are refused (SSRF protection).
/// </summary>
public class WebhookDispatcher : BackgroundService
{
    private static readonly TimeSpan[] Backoff = { TimeSpan.Zero, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(15) };

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(8),
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            var allowed = addresses.FirstOrDefault(a => !IsPrivate(a))
                ?? throw new HttpRequestException("The address resolves to a private or local network, which is not allowed.");
            var socket = new Socket(allowed.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(allowed, context.DnsEndPoint.Port), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }) { Timeout = TimeSpan.FromSeconds(15) };

    private readonly WebhookPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(WebhookPublisher publisher, IServiceScopeFactory scopeFactory, ILogger<WebhookDispatcher> logger)
    {
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    internal static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }
        var b = ip.GetAddressBytes();
        return b[0] == 10 || b[0] == 0 || b[0] == 127
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] >= 224;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in _publisher.Reader.ReadAllAsync(stoppingToken))
            {
                //>>> one event never blocks the next
                _ = Task.Run(() => HandleAsync(evt, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(WebhookEvent evt, CancellationToken token)
    {
        try
        {
            List<WebhookEndpoint> endpoints;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                endpoints = await context.WebhookEndpoint.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync(token);
            }

            foreach (var endpoint in endpoints)
            {
                if (evt.TargetEndpointId != null)
                {
                    if (endpoint.Id != evt.TargetEndpointId) continue;
                }
                else
                {
                    var subscribed = (endpoint.Events ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (!subscribed.Contains("*") && !subscribed.Contains(evt.Name)) continue;
                }

                await DeliverAsync(endpoint, evt, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook dispatch failed for {Event}", evt.Name);
        }
    }

    private async Task DeliverAsync(WebhookEndpoint endpoint, WebhookEvent evt, CancellationToken token)
    {
        var deliveryId = Guid.NewGuid().ToString("N");
        var body = JsonSerializer.Serialize(new { id = deliveryId, @event = evt.Name, createdAt = DateTime.UtcNow, data = evt.Data });
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(endpoint.Secret ?? ""), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        int? status = null;
        string? error = null;
        var success = false;
        var attempts = 0;

        foreach (var delay in Backoff)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            attempts++;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Add("X-Interlace-Event", evt.Name);
                request.Headers.Add("X-Interlace-Delivery", deliveryId);
                request.Headers.Add("X-Interlace-Signature", signature);
                using var response = await Client.SendAsync(request, token);
                status = (int)response.StatusCode;
                success = response.IsSuccessStatusCode;
                error = success ? null : $"HTTP {status}";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
            }

            if (success) break;
            //>>> a client error other than 408/429 will not get better by retrying
            if (status is >= 400 and < 500 && status != 408 && status != 429) break;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            context.WebhookDelivery.Add(new WebhookDelivery
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                EventName = evt.Name,
                Success = success,
                HttpStatus = status,
                Attempts = attempts,
                Error = error != null && error.Length > 900 ? error.Substring(0, 900) : error,
                DeliveredAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not log webhook delivery");
        }
    }
}
