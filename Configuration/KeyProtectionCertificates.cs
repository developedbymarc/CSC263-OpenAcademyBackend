using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenAcademy.Configuration;

internal static class KeyProtectionCertificates
{
    public static X509Certificate2 LoadPair(string certPath, string keyPath)
    {
        try
        {
            return X509Certificate2.CreateFromPemFile(certPath, keyPath);
        }
        catch (Exception e) when (e is IOException or CryptographicException or ArgumentException)
        {
            // Names the files and the error type only, never any key material
            throw new InvalidOperationException(
                $"Could not load certificate '{certPath}' with private key '{keyPath}' ({e.GetType().Name}).", e);
        }
    }

    /// <summary>Retired pairs live as {name}.crt + {name}.key in one directory.</summary>
    public static IEnumerable<(string Crt, string Key)> RetiredPairs(string directory) =>
        Directory.EnumerateFiles(directory, "*.crt").Order()
            .Select(crt => (crt, Path.ChangeExtension(crt, ".key")));
}