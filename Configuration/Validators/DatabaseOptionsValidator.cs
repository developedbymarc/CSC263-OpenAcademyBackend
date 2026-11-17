using Microsoft.Extensions.Options;
using Npgsql;
using OpenAcademy.Configuration.Options;

namespace OpenAcademy.Configuration.Validators;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString)) return ValidateOptionsResult.Success;

        if (!IsValidConnectionString(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                $"The {nameof(DatabaseOptions.ConnectionString)} is not a valid PostgreSQL connection string.");
        }

        if (options.MigratorConnectionString is not null && !IsValidConnectionString(options.MigratorConnectionString))
        {
            return ValidateOptionsResult.Fail(
                $"The {nameof(DatabaseOptions.MigratorConnectionString)} is not a valid PostgreSQL connection string.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(builder.Host) && !string.IsNullOrWhiteSpace(builder.Database);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return false;
        }
    }
}
