using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public record PetParams(
    string Name,
    string? Description,
    decimal? Weight,
    string Species,
    string? Breed);

public class PetService(ApplicationDbContext db)
{
    public async Task<List<Pet>> GetByUserIdAsync(string userId)
    {
        return await db.Pets
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Pet?> GetByIdAsync(int id)
    {
        return await db.Pets
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Pet> CreateAsync(string userId, PetParams p)
    {
        var pet = new Pet
        {
            UserId = userId,
            Name = p.Name,
            Description = p.Description,
            Weight = p.Weight,
            Species = p.Species,
            Breed = p.Breed
        };

        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        return pet;
    }

    public async Task UpdateAsync(Pet pet, PetParams p)
    {
        pet.Name = p.Name;
        pet.Description = p.Description;
        pet.Weight = p.Weight;
        pet.Species = p.Species;
        pet.Breed = p.Breed;
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var pet = await db.Pets.FindAsync(id);
        if (pet is null) return false;

        db.Pets.Remove(pet);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<Pet>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        // Split into words, quote each term for safe FTS5 syntax, and add prefix matching
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ftsQuery = string.Join(" ", terms.Select(t => $"\"{t.Replace("\"", "")}\"*"));

        // EF parameterizes {ftsQuery} as @p0 — the value is bound, not interpolated into SQL
        var petIds = await db.Database
            .SqlQuery<int>($"SELECT rowid AS [Value] FROM PetsFts WHERE PetsFts MATCH {ftsQuery} ORDER BY rank")
            .ToListAsync();

        if (petIds.Count == 0) return [];

        return await db.Pets
            .Where(p => petIds.Contains(p.Id))
            .Include(p => p.User)
            .ToListAsync();
    }
}
