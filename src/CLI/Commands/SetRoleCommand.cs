using System.CommandLine;
using Server.Data;

namespace CLI.Commands;

public static class SetRoleCommand
{
    public static Command Build(Option<string> dbOption)
    {
        var emailOption = new Option<string>("--email") { Description = "Email of the user", Required = true };
        var roleOption = new Option<string[]>("--role") { Description = "Roles to assign (President, Treasurer, Secretary, Resident). Can specify multiple.", Required = true };

        var command = new Command("set-role") { Description = "Set roles for a user by email" };
        command.Options.Add(emailOption);
        command.Options.Add(roleOption);
        command.Options.Add(dbOption);

        command.SetAction(async (parseResult) =>
        {
            var email = parseResult.GetValue(emailOption)!;
            var roles = parseResult.GetValue(roleOption)!;
            var dbPath = parseResult.GetValue(dbOption)!;

            var invalid = roles.Where(r => !Roles.All.Contains(r)).ToArray();
            if (invalid.Length > 0)
            {
                Console.Error.WriteLine($"Invalid role(s): {string.Join(", ", invalid)}. Must be one of: {string.Join(", ", Roles.All)}");
                return;
            }
            if (!roles.Contains(Roles.Resident))
                roles = [.. roles, Roles.Resident];

            await using var svc = await AppServices.CreateAsync(dbPath);

            var user = await svc.UserManager.FindByEmailAsync(email);
            if (user is null)
            {
                Console.Error.WriteLine($"No user found with email '{email}'.");
                return;
            }

            var currentRoles = await svc.UserManager.GetRolesAsync(user);
            var removeResult = await svc.UserManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                Console.Error.WriteLine("Failed to remove existing roles:");
                foreach (var error in removeResult.Errors)
                    Console.Error.WriteLine($"  - {error.Description}");
                return;
            }

            var addResult = await svc.UserManager.AddToRolesAsync(user, roles);
            if (!addResult.Succeeded)
            {
                Console.Error.WriteLine("Failed to add roles:");
                foreach (var error in addResult.Errors)
                    Console.Error.WriteLine($"  - {error.Description}");
                return;
            }

            Console.WriteLine($"Set roles for '{user.FullName}' ({email}): {string.Join(", ", roles)}");
        });

        return command;
    }
}
