using System.CommandLine;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace CLI.Commands;

public static class ImportCsvCommand
{
    public static Command Build(Option<string> dbOption)
    {
        var csvOption = new Option<string[]>("--file") { Description = "Path to CSV file(s) to import", Required = true };

        var command = new Command("import-csv") { Description = "Import residents from CSV file(s). Skips rows with no email. Updates existing users, creates new ones without sending invitations." };
        command.Options.Add(csvOption);
        command.Options.Add(dbOption);

        command.SetAction(async (parseResult) =>
        {
            var csvPaths = parseResult.GetValue(csvOption)!;
            var dbPath = parseResult.GetValue(dbOption)!;

            await using var svc = await AppServices.CreateAsync(dbPath);

            var residentsByEmail = new Dictionary<string, (string FullName, string Phone, List<string> Addresses)>(StringComparer.OrdinalIgnoreCase);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null
            };

            foreach (var csvPath in csvPaths)
            {
                if (!File.Exists(csvPath))
                {
                    Console.Error.WriteLine($"File not found: {csvPath}");
                    return;
                }

                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, csvConfig);
                csv.Context.RegisterClassMap<ResidentCsvMap>();

                foreach (var record in csv.GetRecords<ResidentCsvRow>())
                {
                    if (string.IsNullOrWhiteSpace(record.Email)) continue;

                    if (residentsByEmail.TryGetValue(record.Email, out var existing))
                    {
                        if (!string.IsNullOrWhiteSpace(record.Address) &&
                            !existing.Addresses.Contains(record.Address, StringComparer.OrdinalIgnoreCase))
                        {
                            existing.Addresses.Add(record.Address);
                        }
                        if (string.IsNullOrWhiteSpace(existing.Phone) && !string.IsNullOrWhiteSpace(record.Phone))
                        {
                            residentsByEmail[record.Email] = (existing.FullName, record.Phone, existing.Addresses);
                        }
                    }
                    else
                    {
                        var addresses = new List<string>();
                        if (!string.IsNullOrWhiteSpace(record.Address))
                            addresses.Add(record.Address);
                        residentsByEmail[record.Email] = (record.FullName, record.Phone, addresses);
                    }
                }
            }

            var created = 0;
            var updated = 0;

            foreach (var (email, resident) in residentsByEmail)
            {
                var user = await svc.UserManager.FindByEmailAsync(email);

                if (user is not null)
                {
                    user.FullName = resident.FullName;
                    if (!string.IsNullOrWhiteSpace(resident.Phone))
                        user.PhoneNumber = resident.Phone;

                    await svc.UserManager.UpdateAsync(user);

                    var existingAddresses = await svc.Db.Addresses
                        .Where(a => a.UserId == user.Id)
                        .ToListAsync();

                    foreach (var addr in resident.Addresses)
                    {
                        if (!existingAddresses.Any(a => a.StreetAddress.Equals(addr, StringComparison.OrdinalIgnoreCase)))
                        {
                            svc.Db.Addresses.Add(new Address { UserId = user.Id, StreetAddress = addr });
                        }
                    }
                    await svc.Db.SaveChangesAsync();

                    updated++;
                    Console.WriteLine($"  Updated: {resident.FullName} ({email})");
                }
                else
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FullName = resident.FullName,
                        PhoneNumber = resident.Phone
                    };

                    var result = await svc.UserManager.CreateAsync(newUser);
                    if (!result.Succeeded)
                    {
                        Console.Error.WriteLine($"  Failed to create {resident.FullName} ({email}):");
                        foreach (var error in result.Errors)
                            Console.Error.WriteLine($"    - {error.Description}");
                        continue;
                    }

                    await svc.UserManager.AddToRoleAsync(newUser, Roles.Resident);

                    foreach (var addr in resident.Addresses)
                        svc.Db.Addresses.Add(new Address { UserId = newUser.Id, StreetAddress = addr });
                    await svc.Db.SaveChangesAsync();

                    created++;
                    Console.WriteLine($"  Created: {resident.FullName} ({email})");
                }
            }

            Console.WriteLine($"\nDone. Created: {created}, Updated: {updated}, Skipped (no email): not counted.");
        });

        return command;
    }
}

class ResidentCsvRow
{
    private string? _name, _firstName, _address, _phone, _email;
    public string Name { get => _name?.Trim() ?? ""; set => _name = value; }
    public string FirstName { get => _firstName?.Trim() ?? ""; set => _firstName = value; }
    public string Address { get => _address?.Trim() ?? ""; set => _address = value; }
    public string Phone { get => _phone?.Trim() ?? ""; set => _phone = value; }
    public string Email { get => _email?.Trim() ?? ""; set => _email = value; }
    public string FullName => $"{FirstName} {Name}";
}

sealed class ResidentCsvMap : ClassMap<ResidentCsvRow>
{
    public ResidentCsvMap()
    {
        Map(m => m.Name).Name("NAME");
        Map(m => m.FirstName).Name("FIRST NAME");
        Map(m => m.Address).Name("ADDRESS");
        Map(m => m.Phone).Name("PHONE");
        Map(m => m.Email).Name("EMAIL");
    }
}
