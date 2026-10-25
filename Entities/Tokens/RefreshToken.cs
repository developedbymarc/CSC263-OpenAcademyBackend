using System.ComponentModel.DataAnnotations;
using OpenAcademy.Entities.Users;

namespace OpenAcademy.Entities.Tokens;

public class RefreshToken
{
    // identification
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid FamilyId { get; set; }
    public Guid? ReplacedById { get; set; }
    [Required] [MaxLength(128)] public string TokenHash { get; set; } = null!;

    // metadata
    [Required] public bool IsPersistent { get; set; }
    [Required] public DateTimeOffset CreatedAt { get; set; }
    [Required] public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; } = null;


    public bool IsExpired => ExpiresAt < DateTimeOffset.Now;
    public bool IsRevoked => RevokedAt != null;


    // tracking
    [MaxLength(45)] public string Ip { get; set; } = null!; // 45 = longest textual IPv6
    [MaxLength(512)] public string UserAgent { get; set; } = null!;
}