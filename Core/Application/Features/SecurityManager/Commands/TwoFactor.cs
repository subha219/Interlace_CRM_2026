using Application.Common.Services.SecurityManager;
using FluentValidation;
using MediatR;

namespace Application.Features.SecurityManager.Commands;

public class GetTwoFactorSetupResult { public TwoFactorSetupDto? Data { get; init; } }
public class GetTwoFactorSetupRequest : IRequest<GetTwoFactorSetupResult>
{
    public string? UserId { get; init; }
}
public class GetTwoFactorSetupHandler : IRequestHandler<GetTwoFactorSetupRequest, GetTwoFactorSetupResult>
{
    private readonly ISecurityService _securityService;
    public GetTwoFactorSetupHandler(ISecurityService securityService) { _securityService = securityService; }
    public async Task<GetTwoFactorSetupResult> Handle(GetTwoFactorSetupRequest request, CancellationToken cancellationToken)
        => new() { Data = await _securityService.GetTwoFactorSetupAsync(request.UserId ?? "", cancellationToken) };
}

public class EnableTwoFactorResult { public List<string>? RecoveryCodes { get; init; } }
public class EnableTwoFactorRequest : IRequest<EnableTwoFactorResult>
{
    public string? UserId { get; init; }
    public string? Code { get; init; }
}
public class EnableTwoFactorValidator : AbstractValidator<EnableTwoFactorRequest>
{
    public EnableTwoFactorValidator() { RuleFor(x => x.Code).NotEmpty(); }
}
public class EnableTwoFactorHandler : IRequestHandler<EnableTwoFactorRequest, EnableTwoFactorResult>
{
    private readonly ISecurityService _securityService;
    public EnableTwoFactorHandler(ISecurityService securityService) { _securityService = securityService; }
    public async Task<EnableTwoFactorResult> Handle(EnableTwoFactorRequest request, CancellationToken cancellationToken)
        => new() { RecoveryCodes = await _securityService.EnableTwoFactorAsync(request.UserId ?? "", request.Code ?? "", cancellationToken) };
}

public class DisableTwoFactorResult { public string? Data { get; init; } }
public class DisableTwoFactorRequest : IRequest<DisableTwoFactorResult>
{
    public string? UserId { get; init; }
    public string? Password { get; init; }
}
public class DisableTwoFactorValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorValidator() { RuleFor(x => x.Password).NotEmpty(); }
}
public class DisableTwoFactorHandler : IRequestHandler<DisableTwoFactorRequest, DisableTwoFactorResult>
{
    private readonly ISecurityService _securityService;
    public DisableTwoFactorHandler(ISecurityService securityService) { _securityService = securityService; }
    public async Task<DisableTwoFactorResult> Handle(DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        await _securityService.DisableTwoFactorAsync(request.UserId ?? "", request.Password ?? "", cancellationToken);
        return new() { Data = "Two-factor authentication disabled" };
    }
}
