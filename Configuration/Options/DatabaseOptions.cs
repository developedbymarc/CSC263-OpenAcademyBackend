using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const int MaxCommandTimeoutInSeconds = 300;
    public const int MaxMaxRetryCount = 10;
    public const int MaxMaxRetryDelayInSeconds = 30;
    
    [Range(1, MaxMaxRetryCount)]
    public int MaxRetryCount { get; init; } = 5;
    
    [Range(1, MaxMaxRetryDelayInSeconds)]
    public int MaxRetryDelayInSeconds { get; init; } = 15;

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, MaxCommandTimeoutInSeconds)]
    public int CommandTimeoutInSeconds { get; init; } = 30;
    
    public TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(MaxRetryDelayInSeconds);
}
