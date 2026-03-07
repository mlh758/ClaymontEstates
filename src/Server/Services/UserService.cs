using System.Text;
using FluentEmail.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db, IFluentEmail email)
{
    private IQueryable<ApplicationUser> UsersWithRoles => db.Users
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role);

    public async Task<List<ApplicationUser>> GetAllUsersWithRolesAsync(string? query = null)
    {
        var usersQuery = UsersWithRoles;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.FullName.ToLower().Contains(lowerQuery)
                || u.StreetAddress.ToLower().Contains(lowerQuery));
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

    public async Task<(bool Success, string[] Errors)> CreateUserAsync(
        string fullName, string emailAddress, string phone, string streetAddress, string baseUrl)
    {
        var user = new ApplicationUser
        {
            UserName = emailAddress,
            Email = emailAddress,
            FullName = fullName,
            PhoneNumber = phone,
            StreetAddress = streetAddress,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        await userManager.AddToRoleAsync(user, Roles.Resident);
        await SendPasswordResetEmailAsync(user, baseUrl, isNewAccount: true);

        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateUserAsync(
        ApplicationUser user, string fullName, string email, string phone, string streetAddress, bool showContactInfo)
    {
        user.FullName = fullName;
        user.Email = email;
        user.UserName = email;
        user.PhoneNumber = phone;
        user.StreetAddress = streetAddress;
        user.ShowContactInfo = showContactInfo;

        var result = await userManager.UpdateAsync(user);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
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
              <p>If you did not expect this email, you can safely ignore it.</p>
              """
            : $"""
              <h2>Password Reset</h2>
              <p>Hi {user.FullName},</p>
              <p>A password reset was requested for your Claymont Estates HOA account.</p>
              <p>Please click the link below to reset your password:</p>
              <p><a href="{resetLink}">Reset Your Password</a></p>
              <p>If you did not request this, you can safely ignore it.</p>
              """;

        await email
            .To(user.Email!)
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();
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
