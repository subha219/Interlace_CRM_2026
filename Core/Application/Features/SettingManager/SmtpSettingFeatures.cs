using Application.Common.Services.EmailManager;
using FluentValidation;
using MediatR;

namespace Application.Features.SettingManager;

public class GetSmtpSettingsResult { public SmtpSettingsDto? Data { get; init; } }
public class GetSmtpSettingsRequest : IRequest<GetSmtpSettingsResult> { }

public class GetSmtpSettingsHandler : IRequestHandler<GetSmtpSettingsRequest, GetSmtpSettingsResult>
{
    private readonly ISmtpSettingsService _service;
    public GetSmtpSettingsHandler(ISmtpSettingsService service) { _service = service; }

    public async Task<GetSmtpSettingsResult> Handle(GetSmtpSettingsRequest request, CancellationToken cancellationToken) =>
        new() { Data = await _service.GetAsync(cancellationToken) };
}

public class SaveSmtpSettingsResult { public string? Data { get; init; } }

public class SaveSmtpSettingsRequest : IRequest<SaveSmtpSettingsResult>
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? UserName { get; init; }

    /// <summary>Leave empty to keep the saved password.</summary>
    public string? Password { get; init; }
    public string? FromAddress { get; init; }
    public string? FromName { get; init; }
    public string? Security { get; init; }
    public string? UserId { get; set; }
}

public class SaveSmtpSettingsValidator : AbstractValidator<SaveSmtpSettingsRequest>
{
    public SaveSmtpSettingsValidator()
    {
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.FromAddress).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.FromAddress));
    }
}

public class SaveSmtpSettingsHandler : IRequestHandler<SaveSmtpSettingsRequest, SaveSmtpSettingsResult>
{
    private readonly ISmtpSettingsService _service;
    public SaveSmtpSettingsHandler(ISmtpSettingsService service) { _service = service; }

    public async Task<SaveSmtpSettingsResult> Handle(SaveSmtpSettingsRequest request, CancellationToken cancellationToken)
    {
        await _service.SaveAsync(new SmtpSettingsInput(request.Host, request.Port, request.UserName, request.Password, request.FromAddress, request.FromName, request.Security), request.UserId, cancellationToken);
        return new SaveSmtpSettingsResult { Data = "Saved" };
    }
}

public class SendTestEmailResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public class SendTestEmailRequest : IRequest<SendTestEmailResult>
{
    public string? To { get; init; }
}

public class SendTestEmailValidator : AbstractValidator<SendTestEmailRequest>
{
    public SendTestEmailValidator() { RuleFor(x => x.To).NotEmpty().EmailAddress(); }
}

public class SendTestEmailHandler : IRequestHandler<SendTestEmailRequest, SendTestEmailResult>
{
    private readonly IEmailService _emailService;
    public SendTestEmailHandler(IEmailService emailService) { _emailService = emailService; }

    public async Task<SendTestEmailResult> Handle(SendTestEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _emailService.SendEmailWithResultAsync(
            request.To!, "Interlace test e-mail",
            "<p>This is a test message from Interlace. Your e-mail settings work.</p>");
        return new SendTestEmailResult { Success = result.Success, Error = result.Error };
    }
}
