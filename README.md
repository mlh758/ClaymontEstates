# Basic website for keeping records for the Claymont Estates HOA

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


## Models

Users represent residents of the HOA. The possible roles for users are President, Secretary, Treasurer, and Resident. Everyone but Resident is an Officer and has additional priviledges in the app.

Officers are expected to invite new residents and remove old ones. Officers can also maintain documents for the HOA and manage events.

Events can be configured to be public (appearing on the home page), or private. They can also be configured to enable RSVP functionality which will allow users to indicate their intention to attend.

Most officer actions are logged into an Audit table. It's unlikely we'll ever need to worry about this, but it would be helpful to know if someone is vandalizing someone else's account if there is ever a dispute.


## Future Improvements

- **Radzen.Blazor** — look into adopting this component library for a more polished UI. It includes a free HtmlEditor (WYSIWYG) that would replace the plain textarea for event descriptions, so officers don't need to write HTML. Adopting it would likely mean restyling the whole app to use Radzen components for consistency.