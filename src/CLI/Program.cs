using System.CommandLine;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;

var rootCommand = new RootCommand("HoaSite CLI");

// create-user command
var nameOption = new Option<string>("--name") { Description = "Full name", Required = true };
var emailOption = new Option<string>("--email") { Description = "Email address", Required = true };
var phoneOption = new Option<string>("--phone") { Description = "Phone number" };
var addressOption = new Option<string>("--address") { Description = "Street address", Required = true };
var passwordOption = new Option<string>("--password") { Description = "Password", Required = true };
var roleOption = new Option<string[]>("--role") { Description = "Roles (President, Treasurer, Secretary, Resident). Can specify multiple.", DefaultValueFactory = _ => new[] { Roles.Resident } };
var dbOption = new Option<string>("--db") { Description = "Path to SQLite database file", Required = true };

var createUserCommand = new Command("create-user") { Description = "Create a new user account" };
createUserCommand.Options.Add(nameOption);
createUserCommand.Options.Add(emailOption);
createUserCommand.Options.Add(phoneOption);
createUserCommand.Options.Add(addressOption);
createUserCommand.Options.Add(passwordOption);
createUserCommand.Options.Add(roleOption);
createUserCommand.Options.Add(dbOption);

createUserCommand.SetAction(async (parseResult) =>
{
    var fullName = parseResult.GetValue(nameOption)!;
    var email = parseResult.GetValue(emailOption)!;
    var phone = parseResult.GetValue(phoneOption) ?? "";
    var address = parseResult.GetValue(addressOption)!;
    var password = parseResult.GetValue(passwordOption)!;
    var roles = parseResult.GetValue(roleOption)!;
    var dbPath = parseResult.GetValue(dbOption)!;

    var invalid = roles.Where(r => !Roles.All.Contains(r)).ToArray();
    if (invalid.Length > 0)
    {
        Console.Error.WriteLine($"Invalid role(s): {string.Join(", ", invalid)}. Must be one of: {string.Join(", ", Roles.All)}");
        return;
    }
    // Always include Resident
    if (!roles.Contains(Roles.Resident))
        roles = [.. roles, Roles.Resident];

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite($"DataSource={dbPath}"));
    services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

    await using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var r in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(r))
            await roleManager.CreateAsync(new IdentityRole(r));
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var existing = await userManager.FindByEmailAsync(email);
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
        PhoneNumber = phone,
        StreetAddress = address
    };

    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
        Console.Error.WriteLine("Failed to create user:");
        foreach (var error in result.Errors)
            Console.Error.WriteLine($"  - {error.Description}");
        return;
    }

    await userManager.AddToRolesAsync(user, roles);
    Console.WriteLine($"Created user '{fullName}' ({email}) with roles: {string.Join(", ", roles)}.");
});

rootCommand.Subcommands.Add(createUserCommand);

return await rootCommand.Parse(args).InvokeAsync();
