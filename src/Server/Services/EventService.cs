using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class EventSummary
{
    public required Event Event { get; init; }
    public int RsvpCount { get; set; }
}

public record EventParams(
    string Name,
    string Description,
    DateTime Date,
    TimeSpan Duration,
    string? Location,
    bool IsPublic,
    bool RequiresRsvp);

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

    public async Task<Event> CreateAsync(EventParams p, string createdByEmail)
    {
        var evt = new Event
        {
            Name = p.Name,
            Description = sanitizer.Sanitize(p.Description),
            Date = p.Date,
            Duration = p.Duration,
            Location = p.Location,
            IsPublic = p.IsPublic,
            RequiresRsvp = p.RequiresRsvp,
            CreatedByEmail = createdByEmail
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync();
        return evt;
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(Event evt, EventParams p)
    {
        evt.Name = p.Name;
        evt.Description = sanitizer.Sanitize(p.Description);
        evt.Date = p.Date;
        evt.Duration = p.Duration;
        evt.Location = p.Location;
        evt.IsPublic = p.IsPublic;
        evt.RequiresRsvp = p.RequiresRsvp;

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
        var exists = await db.Events.AnyAsync(e => e.Id == eventId);
        if (!exists)
            return (false, "Event not found.");

        var existing = await db.Rsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);
        if (existing is not null)
            return (true, null);

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
