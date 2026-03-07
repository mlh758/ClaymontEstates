# Basic website for keeping records for the Claymont Estates HOA

## Test User

I created a test user admin@example.com Password12!!


## Setup

The application environment can be set up using nix although this complicates running some of the commands.

### EF Migrations

The nix shell wraps `dotnet`, so `dotnet-ef` can't find the runtime. Set `DOTNET_ROOT` to the actual SDK path before running ef commands:

```bash
export PATH="$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$(dirname "$(readlink -f "$(which dotnet)")")"
```

Then run migrations from the Server project directory:

```bash
cd src/Server
dotnet ef migrations add <MigrationName>
dotnet ef database update
```