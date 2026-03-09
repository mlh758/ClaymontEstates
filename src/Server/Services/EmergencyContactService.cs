using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class EmergencyContactService(ApplicationDbContext db)
{
    public async Task<List<EmergencyContact>> GetByUserIdAsync(string userId)
    {
        return await db.EmergencyContacts
            .Where(ec => ec.UserId == userId)
            .OrderBy(ec => ec.Name)
            .ToListAsync();
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

    public async Task<EmergencyContact> CreateAsync(string userId, string name, string? phoneNumber, string? email, string? address)
    {
        var contact = new EmergencyContact
        {
            UserId = userId,
            Name = name,
            PhoneNumber = phoneNumber,
            Email = email,
            Address = address
        };

        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync();
        return contact;
    }

    public async Task UpdateAsync(EmergencyContact contact, string name, string? phoneNumber, string? email, string? address)
    {
        contact.Name = name;
        contact.PhoneNumber = phoneNumber;
        contact.Email = email;
        contact.Address = address;
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
