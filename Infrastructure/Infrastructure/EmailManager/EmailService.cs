using Application.Common.Services.EmailManager;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Infrastructure.EmailManager;


public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpSettingsService _settings;

    public EmailService(ILogger<EmailService> logger, SmtpSettingsService settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await SendEmailWithResultAsync(email, subject, htmlMessage);
    }

    public async Task<EmailSendResult> SendEmailWithResultAsync(string email, string subject, string htmlMessage, string? replyTo = null, string? fromName = null, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        try
        {
            var smtp = await _settings.ResolveAsync();

            if (string.IsNullOrWhiteSpace(smtp.Host))
            {
                return new EmailSendResult(false, "E-mail is not set up yet. Open Settings > Email Settings and enter your SMTP server.");
            }

            var fromAddress = !string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.FromAddress : smtp.UserName;
            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                return new EmailSendResult(false, "No sender address: set 'From address' or a user name in Email Settings.");
            }

            var displayName = !string.IsNullOrWhiteSpace(fromName) ? fromName
                : !string.IsNullOrWhiteSpace(smtp.FromName) ? smtp.FromName : fromAddress;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(displayName, fromAddress));
            if (!string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            }
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            foreach (var a in attachments ?? Array.Empty<EmailAttachment>())
                builder.Attachments.Add(a.FileName, a.Content, MimeKit.ContentType.Parse(a.ContentType));
            message.Body = builder.ToMessageBody();

            var options = smtp.Security switch
            {
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                "StartTls" => SecureSocketOptions.StartTls,
                "None" => SecureSocketOptions.None,
                _ => SecureSocketOptions.Auto
            };

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                client.Timeout = 20000;
                await client.ConnectAsync(smtp.Host, smtp.Port, options);
                if (!string.IsNullOrWhiteSpace(smtp.UserName))
                {
                    await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty);
                }
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            return new EmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email.");
            return new EmailSendResult(false, ex.Message);
        }
    }
}
