namespace Application.Common.Services.SecurityManager;

public record TwoFactorSetupDto
{
    public bool Enabled { get; init; }
    public string? SharedKey { get; init; }
    public string? AuthenticatorUri { get; init; }
}
