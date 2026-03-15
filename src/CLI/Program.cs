using System.CommandLine;
using CLI.Commands;

var dbOption = new Option<string>("--db") { Description = "Path to SQLite database file", Required = true };

var rootCommand = new RootCommand("HoaSite CLI");
rootCommand.Subcommands.Add(CreateUserCommand.Build(dbOption));
rootCommand.Subcommands.Add(ImportCsvCommand.Build(dbOption));
rootCommand.Subcommands.Add(SetRoleCommand.Build(dbOption));

return await rootCommand.Parse(args).InvokeAsync();
