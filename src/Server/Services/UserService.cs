using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public record AddressParams(
    string StreetAddress,
    string City = "",
    string State = "",
    string Zip = "",
    bool IsPrivate = false);

public record UserParams(
    string FullName,
    string? Email,
    string Phone,
    List<AddressParams> Addresses,
    bool ShowContactInfo = true,
    bool WantsEmailNotifications = true);

public class UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db, EmailOutboxService outbox)
{
    private IQueryable<ApplicationUser> UsersWithRolesAndAddresses => db.Users
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
        .Include(u => u.Addresses);

    public async Task<List<ApplicationUser>> GetAllUsersWithRolesAsync(string? query = null)
    {
        var usersQuery = UsersWithRolesAndAddresses;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.FullName.ToLower().Contains(lowerQuery)
                || u.Addresses.Any(a => a.StreetAddress.ToLower().Contains(lowerQuery)));
        }

        return await usersQuery.OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<List<(ApplicationUser User, string? DisplayRoles)>> SearchUsersWithRolesAsync(string? query)
    {
        var users = await GetAllUsersWithRolesAsync(query);
        return users.Select(u =>
        {
            var officerRoles = u.RoleNames.Where(r => Roles.OfficerRoles.Contains(r)).ToList();
            var display = officerRoles.Count > 0 ? string.Join(", ", officerRoles) : null;
            return (u, display);
        }).ToList();
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
        return await userManager.FindByIdAsync(id);
    }

    public async Task<(bool Success, string[] Errors)> CreateUserAsync(UserParams p, string baseUrl)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(p.Email);
        var user = new ApplicationUser
        {
            UserName = hasEmail ? p.Email : Guid.NewGuid().ToString(),
            Email = hasEmail ? p.Email : null,
            FullName = p.FullName,
            PhoneNumber = p.Phone,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        foreach (var a in p.Addresses.Where(a => !string.IsNullOrWhiteSpace(a.StreetAddress)))
            db.Addresses.Add(new Address
            {
                UserId = user.Id,
                StreetAddress = a.StreetAddress.Trim(),
                City = a.City.Trim(),
                State = a.State.Trim(),
                Zip = a.Zip.Trim(),
                IsPrivate = a.IsPrivate
            });
        await db.SaveChangesAsync();

        await userManager.AddToRoleAsync(user, Roles.Resident);

        if (hasEmail)
            await SendPasswordResetEmailAsync(user, baseUrl, isNewAccount: true);

        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateUserAsync(ApplicationUser user, UserParams p)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(p.Email);
        user.FullName = p.FullName;
        user.Email = hasEmail ? p.Email : null;
        user.UserName = hasEmail ? p.Email : user.UserName;
        user.PhoneNumber = p.Phone;
        user.ShowContactInfo = p.ShowContactInfo;
        user.WantsEmailNotifications = p.WantsEmailNotifications;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        // Sync addresses: remove old, add new
        var existing = await db.Addresses.Where(a => a.UserId == user.Id).ToListAsync();
        db.Addresses.RemoveRange(existing);
        foreach (var a in p.Addresses.Where(a => !string.IsNullOrWhiteSpace(a.StreetAddress)))
            db.Addresses.Add(new Address
            {
                UserId = user.Id,
                StreetAddress = a.StreetAddress.Trim(),
                City = a.City.Trim(),
                State = a.State.Trim(),
                Zip = a.Zip.Trim(),
                IsPrivate = a.IsPrivate
            });
        await db.SaveChangesAsync();

        return (true, []);
    }

    public async Task<List<Address>> GetUserAddressesAsync(string userId)
    {
        return await db.Addresses.Where(a => a.UserId == userId).ToListAsync();
    }

    public async Task<List<(ApplicationUser User, string SharedAddress)>> GetUsersAtSameAddressesAsync(string userId)
    {
        var userAddresses = await db.Addresses
            .Where(a => a.UserId == userId)
            .Select(a => a.StreetAddress.ToLower())
            .ToListAsync();

        if (userAddresses.Count == 0) return [];

        return await db.Addresses
            .Include(a => a.User)
            .Where(a => a.UserId != userId && userAddresses.Contains(a.StreetAddress.ToLower()))
            .Select(a => new { a.User, a.StreetAddress })
            .AsAsyncEnumerable()
            .Select(a => (a.User, a.StreetAddress))
            .ToListAsync();
    }

    public async Task<IList<string>> GetUserRolesAsync(ApplicationUser user)
    {
        return await userManager.GetRolesAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> SetUserRolesAsync(ApplicationUser user, IEnumerable<string> newRoles)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return (false, removeResult.Errors.Select(e => e.Description).ToArray());

        var addResult = await userManager.AddToRolesAsync(user, newRoles);
        return (addResult.Succeeded, addResult.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> DeleteUserAsync(ApplicationUser user)
    {
        var result = await userManager.DeleteAsync(user);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task SendPasswordResetEmailAsync(ApplicationUser user, string baseUrl, bool isNewAccount = false)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var encodedEmail = Uri.EscapeDataString(user.Email!);
        var resetLink = $"{baseUrl}/Account/ResetPassword?code={encodedToken}&email={encodedEmail}";

        var subject = isNewAccount
            ? "Welcome to Claymont Estates HOA"
            : "Password Reset - Claymont Estates HOA";

        var body = isNewAccount
            ? $"""
              <h2>Welcome to Claymont Estates HOA, {user.FullName}!</h2>
              <p>An account has been created for you on the Claymont Estates community portal.</p>
              <p>Please click the link below to set your password and get started:</p>
              <p><a href="{resetLink}">Set Your Password</a></p>
              <p>This link expires in 1 day. If it has expired, you can request a new one using the password reset option on the login page.</p>
              <p>If you did not expect this email, you can safely ignore it.</p>
              """
            : $"""
              <h2>Password Reset</h2>
              <p>Hi {user.FullName},</p>
              <p>A password reset was requested for your Claymont Estates HOA account.</p>
              <p>Please click the link below to reset your password:</p>
              <p><a href="{resetLink}">Reset Your Password</a></p>
              <p>This link expires in 1 day. If it has expired, you can request a new one from the login page.</p>
              <p>If you did not request this, you can safely ignore it.</p>
              """;

        await outbox.QueueAsync(new EmailMessage(user.Email!, subject, body));

        user.LastPasswordResetSentAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
    }

    public async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
