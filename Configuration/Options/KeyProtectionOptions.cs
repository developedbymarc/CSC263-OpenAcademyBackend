using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class KeyProtectionOptions
{
    public const string SectionName = "KeyProtection";
    
    [Required]
    public string CertificatePath { get; init; } = string.Empty;
    
    [Required]
    public string PrivateKeyPath { get; init; } = string.Empty;
    
    /// <summary>Optional. Holds retired pairs as {name}.crt + {name}.key, still used for decryption.</summary>
    public string? RetiredDirectory { get; init; }
}