using Infrastructure.DataAccessManager.EFCore;
using Infrastructure.AssistantManager;
using Infrastructure.EmailManager;
using Infrastructure.FileDocumentManager;
using Infrastructure.FileImageManager;
using Infrastructure.LogManager.Serilogs;
using Infrastructure.NotificationManager;
using Infrastructure.SecurityManager.AspNetIdentity;
using Infrastructure.SecurityManager.Tokens;
using Infrastructure.SeedManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        //>>> DataAccess
        services.RegisterDataAccess(configuration);

        //>>> Serilog
        services.RegisterSerilog(configuration);

        //>>> Token Manager
        services.RegisterToken(configuration);

        //>>> Security Manager
        services.RegisterSecurityManager(configuration);

        //>>> System Seed Manager
        services.RegisterSystemSeedManager(configuration);

        //>>> Demo Seed Manager
        services.RegisterDemoSeedManager(configuration);

        //>>> DeletedById Manager
        services.RegisterEmailManager(configuration);

        //>>> FileDocumentManager
        services.RegisterFileDocumentManager(configuration);

        //>>> FileImageManager
        services.RegisterFileImageManager(configuration);

        //>>> Assistant (optional LLM backend)
        services.AddScoped<Application.Features.AssistantManager.IAssistantLlm, ClaudeAssistantLlm>();

        //>>> Webhooks: one shared queue, one background dispatcher
        services.AddSingleton<Application.Common.Services.IntegrationManager.IConnectionSettings, Infrastructure.IntegrationManager.ConnectionSettingsService>();
        services.AddSingleton<Infrastructure.IntegrationManager.WebhookPublisher>();
        services.AddSingleton<Application.Common.Services.IntegrationManager.IWebhookPublisher>(sp => sp.GetRequiredService<Infrastructure.IntegrationManager.WebhookPublisher>());
        services.AddHostedService<Infrastructure.IntegrationManager.WebhookDispatcher>();
        services.AddHostedService<Infrastructure.IntegrationManager.WorkflowDispatcher>();

        //>>> Alerts (overdue invoices, SLA breaches, ...)
        services.AddHostedService<AlertBackgroundService>();

        //>>> Google Calendar (CRM activities -> calendar events)
        services.AddSingleton<Infrastructure.CalendarManager.GoogleCalendarClient>();
        services.AddSingleton<Infrastructure.CalendarManager.OutlookCalendarClient>();
        services.AddSingleton<Infrastructure.CalendarManager.CalendarProtector>();
        services.AddScoped<Infrastructure.CalendarManager.CalendarSyncRunner>();
        services.AddHostedService<Infrastructure.CalendarManager.CalendarSyncService>();

        //>>> WhatsApp and SMS
        services.AddSingleton<Application.Features.MessagingManager.IChannelProvider, Infrastructure.MessagingManager.WhatsAppCloudProvider>();
        services.AddSingleton<Application.Features.MessagingManager.IChannelProvider, Infrastructure.MessagingManager.Msg91SmsProvider>();

        services.AddSingleton<Application.Features.TelephonyManager.ICallProvider, Infrastructure.MessagingManager.ExotelCallProvider>();

        //>>> Single sign-on (Google, Microsoft)
        services.AddMemoryCache();
        services.AddSingleton<Infrastructure.SsoManager.SsoService>();

        //>>> Gmail to CRM
        services.AddScoped<Infrastructure.MailSyncManager.MailSyncRunner>();
        services.AddHostedService<Infrastructure.MailSyncManager.MailSyncService>();

        //>>> Scheduled report e-mails
        services.AddHostedService<Infrastructure.ReportManager.ReportScheduler>();
        services.AddHostedService<Infrastructure.AgentManager.AgentScheduler>();

        return services;
    }
}


