using Microsoft.AspNetCore.Identity;

namespace OpenAcademy.Entities.Users;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base()
    {
    }
    
    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}