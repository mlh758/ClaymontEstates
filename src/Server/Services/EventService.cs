using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class EventSummary
{
    public required Event Event { get; init; }
    public int RsvpCount { get; set; }
}

public class EventService(ApplicationDbContext db, HtmlSanitizationService sanitizer)
{
    public async Task<List<Event>> GetRecentAndUpcomingAsync()
    {
        var from = DateTime.UtcNow.Date.AddDays(-30);
        var to = DateTime.UtcNow.Date.AddDays(90);
        return await db.Events
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync();
    }

    public async Task<List<Event>> GetUpcomingPublicAsync()
    {
        return await db.Events
            .Where(e => e.IsPublic && e.Date >= DateTime.UtcNow.Date)
            .OrderBy(e => e.Date)
            .ToListAsync();
    }

    public async Task<List<EventSummary>> GetEventsForMonthAsync(DateTime month)
    {
        var from = new DateTime(month.Year, month.Month, 1);
        var to = from.AddMonths(1);

        return await db.Events
            .Where(e => e.Date >= from && e.Date < to)
            .OrderBy(e => e.Date)
            .Select(e => new EventSummary
            {
                Event = e,
                RsvpCount = e.Rsvps.Count
            })
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetUserRsvpEventIdsAsync(string userId, DateTime from, DateTime to)
    {
        var ids = await db.Rsvps
            .Where(r => r.UserId == userId && r.Event.Date >= from && r.Event.Date < to)
            .Select(r => r.EventId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task<Event?> GetByIdAsync(int id)
    {
        return await db.Events
            .Include(e => e.Rsvps).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Event> CreateAsync(string name, string description, DateTime date, TimeSpan duration, string? location, bool isPublic, bool requiresRsvp, string createdByEmail)
    {
        var evt = new Event
        {
            Name = name,
            Description = sanitizer.Sanitize(description.Replace("\r\n", "<br />").Replace("\n", "<br />")),
            Date = date,
            Duration = duration,
            Location = location,
            IsPublic = isPublic,
            RequiresRsvp = requiresRsvp,
            CreatedByEmail = createdByEmail
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(Event evt, string name, string description, DateTime date, TimeSpan duration, string? location, bool isPublic, bool requiresRsvp)
    {
        evt.Name = name;
        evt.Description = sanitizer.Sanitize(description.Replace("\r\n", "<br />").Replace("\n", "<br />"));
        evt.Date = date;
        evt.Duration = duration;
        evt.Location = location;
        evt.IsPublic = isPublic;
        evt.RequiresRsvp = requiresRsvp;

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var evt = await db.Events.FindAsync(id);
        if (evt is null)
            return (false, "Event not found.");

        db.Events.Remove(evt);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RsvpAsync(int eventId, string userId)
    {
        var evt = await db.Events.FindAsync(eventId);
        if (evt is null)
            return (false, "Event not found.");

        if (!evt.RequiresRsvp)
            return (false, "This event does not require RSVP.");

        var existing = await db.Rsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);
        if (existing is not null)
            return (true, null); // already RSVPed

        db.Rsvps.Add(new Rsvp { EventId = eventId, UserId = userId });
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelRsvpAsync(int eventId, string userId)
    {
        var rsvp = await db.Rsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);
        if (rsvp is null)
            return (false, "RSVP not found.");

        db.Rsvps.Remove(rsvp);
        await db.SaveChangesAsync();
        return (true, null);
    }
}
