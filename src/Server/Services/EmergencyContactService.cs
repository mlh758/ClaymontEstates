using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public record EmergencyContactParams(
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? Relationship);

public class EmergencyContactService(ApplicationDbContext db)
{
    public async Task<List<EmergencyContact>> GetByUserIdAsync(string userId)
    {
        return await db.EmergencyContacts
            .Where(ec => ec.UserId == userId)
            .OrderBy(ec => ec.Name)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetCountsByUserAsync()
    {
        return await db.EmergencyContacts
            .GroupBy(ec => ec.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<List<EmergencyContact>> GetAllAsync()
    {
        return await db.EmergencyContacts
            .Include(ec => ec.User)
            .OrderBy(ec => ec.User.FullName)
            .ThenBy(ec => ec.Name)
            .ToListAsync();
    }

    public async Task<EmergencyContact?> GetByIdAsync(int id)
    {
        return await db.EmergencyContacts
            .Include(ec => ec.User)
            .FirstOrDefaultAsync(ec => ec.Id == id);
    }

    public async Task<EmergencyContact> CreateAsync(string userId, EmergencyContactParams p)
    {
        var contact = new EmergencyContact
        {
            UserId = userId,
            Name = p.Name,
            PhoneNumber = p.PhoneNumber,
            Email = p.Email,
            Address = p.Address,
            Relationship = p.Relationship
        };

        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    public async Task UpdateAsync(EmergencyContact contact, EmergencyContactParams p)
    {
        contact.Name = p.Name;
        contact.PhoneNumber = p.PhoneNumber;
        contact.Email = p.Email;
        contact.Address = p.Address;
        contact.Relationship = p.Relationship;
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var contact = await db.EmergencyContacts.FindAsync(id);
        if (contact is null) return false;

        db.EmergencyContacts.Remove(contact);
        await db.SaveChangesAsync();
        return true;
    }
}
