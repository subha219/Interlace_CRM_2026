using Domain.Common;

namespace Domain.Entities;

/// <summary>The outgoing mail (SMTP) server. One row; the password is stored encrypted.</summary>
public class EmailSetting : BaseEntity
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? UserName { get; set; }

    /// <summary>Password encrypted with ASP.NET Data Protection. Never returned to the browser.</summary>
    public string? PasswordProtected { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }

    /// <summary>Auto, SslOnConnect, StartTls or None.</summary>
    public string? Security { get; set; }
}
