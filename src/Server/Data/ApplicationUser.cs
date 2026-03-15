using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Server.Data;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool ShowContactInfo { get; set; } = true;

    public bool WantsEmailNotifications { get; set; } = true;

    public DateTime? LastPasswordResetSentAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public virtual ICollection<Address> Addresses { get; set; } = [];

    public IEnumerable<string> RoleNames => UserRoles.Select(ur => ur.Role.Name!);
    public string AddressDisplay(bool includePrivate = false) =>
        string.Join(", ", Addresses.Where(a => includePrivate || !a.IsPrivate).Select(a => a.StreetAddress));
}

public class Address
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string StreetAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(2)]
    public string State { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Zip { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ApplicationUserRole : IdentityUserRole<string>
{
    public virtual IdentityRole Role { get; set; } = null!;
}
