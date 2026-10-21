using Application.Common.Services.SecurityManager;
using FluentValidation;
using MediatR;

namespace Application.Features.SecurityManager.Queries;


public class LoginResult
{
    public LoginResultDto? Data { get; init; }
}

public class LoginRequest : IRequest<LoginResult>
{
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? TwoFactorCode { get; init; }
    public bool SendEmailCode { get; init; }
}

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginHandler : IRequestHandler<LoginRequest, LoginResult>
{
    private readonly ISecurityService _securityService;

    public LoginHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<LoginResult> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _securityService.LoginAsync(
            request.Email ?? "",
            request.Password ?? "",
            request.TwoFactorCode,
            request.SendEmailCode,
            cancellationToken
            );

        return new LoginResult
        {
            Data = result
        };
    }
}