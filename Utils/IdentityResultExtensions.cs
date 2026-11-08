using Microsoft.AspNetCore.Identity;

namespace OpenAcademy.Utils;

public static class IdentityResultExtensions
{
    public static IReadOnlyDictionary<string, string[]> ToDetails(this IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
