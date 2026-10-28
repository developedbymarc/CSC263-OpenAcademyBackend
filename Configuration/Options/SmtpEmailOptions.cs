using System.ComponentModel.DataAnnotations;

public enum SmtpSecurityMode
{
    None,
    StartTls,
    StartTlsWhenAvailable,
    ImplicitTls
}

public sealed class SmtpEmailOptions
{
    public const string SectionName = "SmtpEmail";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, ushort.MaxValue)]
    public ushort Port { get; init; } = 1025;

    [Required, EmailAddress]
    public string FromAddress { get; init; } = "noreply@example.test";
    
    public string? FromName { get; init; } = "noreply";

    public SmtpSecurityMode SecurityMode { get; init; } = SmtpSecurityMode.None;

    public string? Username { get; init; }

    public string? Password { get; init; }
}