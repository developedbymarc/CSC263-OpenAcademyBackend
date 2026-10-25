using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenAcademy.Configuration;

public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services)
    {
        AddValidated<JwtOptions, JwtOptionsValidator>(services, JwtOptions.SectionName);
        AddValidated<RefreshTokenOptions, RefreshTokenOptionsValidator>(services, RefreshTokenOptions.SectionName);
        AddValidated<FrontendOptions, FrontendOptionsValidator>(services, FrontendOptions.SectionName);
        AddValidated<DatabaseOptions, DatabaseOptionsValidator>(services, DatabaseOptions.SectionName);
        AddValidated<KeyProtectionOptions, KeyProtectionOptionsValidator>(services, KeyProtectionOptions.SectionName);
        AddValidated<CsrfOptions>(services, CsrfOptions.SectionName);
        AddValidated<S3Options>(services, S3Options.SectionName);

        // Cross-section rules, independent of any single options class
        services.AddOptions<CookieNamesCheck>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<CookieNamesCheck>, CookieNamesValidator>();

        return services;
    }

    private static void AddValidated<TOptions>(IServiceCollection services, string section)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, string section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        AddValidated<TOptions>(services, section);
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
