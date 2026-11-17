using OpenAcademy.Constants;

namespace OpenAcademy.Services.ExternalServices.EmailService;

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = false);

public interface IEmailService
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task SendTemplatedEmailAsync(EEmailTemplate template, object model, string to, CancellationToken cancellationToken = default);
}
