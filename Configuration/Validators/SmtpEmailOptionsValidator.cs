using Microsoft.Extensions.Options;
using System.Net.Mail;

public sealed class SmtpEmailOptionsValidator
    : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        SmtpEmailOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
            errors.Add("SMTP host is required.");

        if (options.Port is < 1 or > 65535)
            errors.Add("SMTP port must be between 1 and 65535.");

        if (!IsValidEmail(options.FromAddress))
            errors.Add("A valid sender email address is required.");

        if (string.IsNullOrWhiteSpace(options.Username) !=
            string.IsNullOrWhiteSpace(options.Password))
        {
            errors.Add(
                "SMTP username and password must be configured together.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsValidEmail(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        try
        {
            return new MailAddress(address).Address == address;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}