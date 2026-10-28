
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace OpenAcademy.Services.ExternalServices.EmailService;

public sealed class EmailService : IEmailService
{
    private readonly SmtpEmailOptions _options;

    public EmailService(IOptions<SmtpEmailOptions> smtpEmailOptions)
    {
        _options = smtpEmailOptions.Value;
    }

    public async Task SendEmailAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(
            new MailboxAddress(
                _options.FromName ?? string.Empty,
                _options.FromAddress));

        mimeMessage.To.Add(
            MailboxAddress.Parse(message.To));

        mimeMessage.Subject = message.Subject;

        mimeMessage.Body = message.IsHtml
            ? new BodyBuilder { HtmlBody = message.Body }.ToMessageBody()
            : new BodyBuilder { TextBody = message.Body }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.SecurityMode switch
            {
                SmtpSecurityMode.None =>
                    SecureSocketOptions.None,

                SmtpSecurityMode.StartTls =>
                    SecureSocketOptions.StartTls,

                SmtpSecurityMode.StartTlsWhenAvailable =>
                    SecureSocketOptions.StartTlsWhenAvailable,

                SmtpSecurityMode.ImplicitTls =>
                    SecureSocketOptions.SslOnConnect,

                _ => throw new InvalidOperationException(
                    "Unsupported SMTP security mode.")
            },
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(
                _options.Username!,
                _options.Password!,
                cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
