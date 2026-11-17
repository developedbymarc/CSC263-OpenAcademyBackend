using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAcademy.Entities.Moderation;

namespace OpenAcademy.Entities.Users.RoleProfiles;

public class RootProfile
{
    [Key] [ForeignKey("User")] public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
