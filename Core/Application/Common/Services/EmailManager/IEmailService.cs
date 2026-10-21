namespace Application.Common.Services.EmailManager;

public record EmailSendResult(bool Success, string? Error);

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailService
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);

    /// <summary>Sends and reports whether it worked (SendEmailAsync swallows errors).</summary>
    Task<EmailSendResult> SendEmailWithResultAsync(string email, string subject, string htmlMessage, string? replyTo = null, string? fromName = null, IReadOnlyList<EmailAttachment>? attachments = null);

}
