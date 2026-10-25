using Microsoft.Extensions.Options;
using Npgsql;

namespace OpenAcademy.Configuration;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString)) return ValidateOptionsResult.Success;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(builder.Host))
                failures.Add("Database:ConnectionString must specify Host.");
            if (string.IsNullOrWhiteSpace(builder.Database))
                failures.Add("Database:ConnectionString must specify Database.");
            // Username/Password are deliberately not required: Npgsql can take them from PGUSER/PGPASSFILE

            return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            // Don't echo the exception or the value: the string contains a password
            return ValidateOptionsResult.Fail("Database:ConnectionString is not a valid Npgsql connection string.");
        }
    }
}
