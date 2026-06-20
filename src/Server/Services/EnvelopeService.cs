using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Server.Data;
using QpdfDocument = QuestPDF.Fluent.Document;

namespace Server.Services;

public class EnvelopeService(ApplicationDbContext db)
{
    static EnvelopeService()
    {
            QuestPDF.Settings.License = LicenseType.Community;
    }

    // A party on an envelope: a (possibly combined) name and its address lines.
    private record Party(string Name, IReadOnlyList<string> AddressLines);

    // Envelope page dimensions in inches (width x height, landscape).
    public record EnvelopeSize(string Key, string Label, float WidthInches, float HeightInches);

    public static readonly EnvelopeSize Number10 = new("num10", "#10 (US, 9½ × 4⅛\")", 9.5f, 4.125f);
    public static readonly EnvelopeSize Dl = new("dl", "DL (220 × 110 mm)", 220f / 25.4f, 110f / 25.4f);
    public static readonly EnvelopeSize Monarch = new("monarch", "Monarch reply (7½ × 3⅞\")", 7.5f, 3.875f);

    public static readonly IReadOnlyList<EnvelopeSize> Sizes = [Number10, Dl, Monarch];

    public static EnvelopeSize ResolveSize(string? key) =>
        Sizes.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Number10;

    /// <summary>
    /// Generates a PDF with one envelope-sized page per household.
    /// Outgoing mailers (returnMailer = false): household is the destination, the
    /// current user is the return address. Return mailers (returnMailer = true): the
    /// household is the return address and the current user is the destination.
    /// </summary>
    public async Task<byte[]> GenerateAsync(string currentUserId, bool returnMailer, EnvelopeSize size)
    {
        var sender = await BuildSenderAsync(currentUserId);
        var households = await BuildHouseholdsAsync(currentUserId);

        return QpdfDocument.Create(doc =>
        {
            if (households.Count == 0)
            {
                doc.Page(page =>
                {
                    ConfigurePage(page, size);
                    page.Content().AlignMiddle().AlignCenter()
                        .Text("No households found in the directory.").FontSize(12);
                });
                return;
            }

            foreach (var household in households)
            {
                var returnParty = returnMailer ? household : sender;
                var destinationParty = returnMailer ? sender : household;

                doc.Page(page =>
                {
                    ConfigurePage(page, size);
                    page.Content().Column(col =>
                    {
                        col.Item().Element(e => RenderAddress(e, returnParty, fontSize: 9));
                        col.Item().Extend().AlignMiddle().AlignCenter()
                            .Element(e => RenderAddress(e, destinationParty, fontSize: 13));
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page, EnvelopeSize size)
    {
        page.Size(size.WidthInches, size.HeightInches, Unit.Inch);
        page.Margin(0.4f, Unit.Inch);
        page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontColor(Colors.Black));
    }

    private static void RenderAddress(IContainer container, Party party, float fontSize)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(party.Name))
                col.Item().Text(party.Name).FontSize(fontSize).SemiBold();

            foreach (var line in party.AddressLines)
                col.Item().Text(line).FontSize(fontSize);
        });
    }

    private async Task<Party> BuildSenderAsync(string currentUserId)
    {
        var user = await db.Users
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user is null)
            return new Party("", []);

        var address = user.Addresses.OrderBy(a => a.Id).FirstOrDefault();
        return new Party(user.FullName, AddressLines(address));
    }

    private async Task<List<Party>> BuildHouseholdsAsync(string currentUserId)
    {
        var addresses = await db.Addresses
            .Include(a => a.User)
            .ToListAsync();

        return addresses
            .Where(a => !string.IsNullOrWhiteSpace(a.StreetAddress))
            .GroupBy(a => NormalizeAddressKey(a.StreetAddress))
            // Skip our own household so we don't mail envelopes to ourselves.
            .Where(g => g.All(a => a.UserId != currentUserId))
            .Select(g =>
            {
                var members = g.Select(a => a.User)
                    .Where(u => u is not null)
                    .DistinctBy(u => u.Id)
                    .ToList();
                return new
                {
                    Sort = g.First().StreetAddress,
                    Party = new Party(FormatHouseholdNames(members), AddressLines(g.First()))
                };
            })
            .OrderBy(x => x.Sort, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Party)
            .ToList();
    }

    private static string NormalizeAddressKey(string streetAddress) =>
        string.Join(' ', streetAddress.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // Residents are clustered in the same area and omit the city/state/zip, so
    // fall back to the neighborhood's defaults when any of those fields are blank.
    private const string DefaultCity = "Kansas City";
    private const string DefaultState = "MO";
    private const string DefaultZip = "64116";

    private static IReadOnlyList<string> AddressLines(Address? address)
    {
        if (address is null)
            return [];

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(address.StreetAddress))
            lines.Add(address.StreetAddress.Trim());

        var city = Coalesce(address.City, DefaultCity);
        var state = Coalesce(address.State, DefaultState);
        var zip = Coalesce(address.Zip, DefaultZip);
        lines.Add($"{city}, {state} {zip}");

        return lines;
    }

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>
    /// Combines the residents of a household into a single addressee line. People who
    /// share a last name are grouped ("Jack and Jill Smith"); people with different
    /// last names are listed by full name ("John Smith and Jane Doe").
    /// </summary>
    private static string FormatHouseholdNames(IReadOnlyList<ApplicationUser> users)
    {
        var ordered = users
            .Where(u => !string.IsNullOrWhiteSpace(u.FullName))
            .OrderBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count == 0)
            return "";

        // Group by last name, preserving first-seen order.
        var groups = new List<(string LastName, List<ApplicationUser> Members)>();
        foreach (var user in ordered)
        {
            var last = LastName(user.FullName);
            var group = groups.FirstOrDefault(g => string.Equals(g.LastName, last, StringComparison.OrdinalIgnoreCase));
            if (group.Members is null)
            {
                group = (last, []);
                groups.Add(group);
            }
            group.Members.Add(user);
        }

        var rendered = groups.Select(g =>
        {
            if (g.Members.Count == 1)
                return g.Members[0].FullName.Trim();

            var firstNames = g.Members.Select(m => FirstName(m.FullName)).ToList();
            return $"{JoinNames(firstNames)} {g.LastName}".Trim();
        }).ToList();

        return JoinNames(rendered);
    }

    private static string LastName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : "";
    }

    private static string FirstName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(' ', parts[..^1]) : fullName.Trim();
    }

    private static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
    };
}
