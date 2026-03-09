using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Server.Data;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string StreetAddress { get; set; } = string.Empty;

    public bool ShowContactInfo { get; set; } = true;

    public bool WantsEmailNotifications { get; set; } = true;

    public DateTime? LastPasswordResetSentAt { get; set; }

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = [];

    public IEnumerable<string> RoleNames => UserRoles.Select(ur => ur.Role.Name!);
}

public class ApplicationUserRole : IdentityUserRole<string>
{
    public virtual IdentityRole Role { get; set; } = null!;
}
