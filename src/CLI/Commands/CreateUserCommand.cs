using System.CommandLine;
using Server.Data;

namespace CLI.Commands;

public static class CreateUserCommand
{
    public static Command Build(Option<string> dbOption)
    {
        var nameOption = new Option<string>("--name") { Description = "Full name", Required = true };
        var emailOption = new Option<string>("--email") { Description = "Email address", Required = true };
        var phoneOption = new Option<string>("--phone") { Description = "Phone number" };
        var addressOption = new Option<string[]>("--address") { Description = "Street address (can specify multiple)", Required = true };
        var passwordOption = new Option<string>("--password") { Description = "Password", Required = true };
        var roleOption = new Option<string[]>("--role") { Description = "Roles (President, Treasurer, Secretary, Resident). Can specify multiple.", DefaultValueFactory = _ => new[] { Roles.Resident } };

        var command = new Command("create-user") { Description = "Create a new user account" };
        command.Options.Add(nameOption);
        command.Options.Add(emailOption);
        command.Options.Add(phoneOption);
        command.Options.Add(addressOption);
        command.Options.Add(passwordOption);
        command.Options.Add(roleOption);
        command.Options.Add(dbOption);

        command.SetAction(async (parseResult) =>
        {
            var fullName = parseResult.GetValue(nameOption)!;
            var email = parseResult.GetValue(emailOption)!;
            var phone = parseResult.GetValue(phoneOption) ?? "";
            var addresses = parseResult.GetValue(addressOption)!;
            var password = parseResult.GetValue(passwordOption)!;
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

            var existing = await svc.UserManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                Console.Error.WriteLine($"A user with email '{email}' already exists.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phone
            };

            var result = await svc.UserManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                Console.Error.WriteLine("Failed to create user:");
                foreach (var error in result.Errors)
                    Console.Error.WriteLine($"  - {error.Description}");
                return;
            }

            foreach (var addr in addresses)
                svc.Db.Addresses.Add(new Address { UserId = user.Id, StreetAddress = addr });
            await svc.Db.SaveChangesAsync();

            await svc.UserManager.AddToRolesAsync(user, roles);
            Console.WriteLine($"Created user '{fullName}' ({email}) with roles: {string.Join(", ", roles)}.");
        });

        return command;
    }
}
