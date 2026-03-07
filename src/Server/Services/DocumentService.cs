using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public class DocumentService(ApplicationDbContext db, IConfiguration config)
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    private string StoragePath => Path.GetFullPath(
        config.GetValue<string>("DocumentStorage:Path")
        ?? Path.Combine(AppContext.BaseDirectory, "Documents"));

    public async Task<List<Document>> GetAllAsync()
    {
        return await db.Documents.OrderByDescending(d => d.EffectiveDate).ToListAsync();
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await db.Documents.FindAsync(id);
    }

    public async Task<(bool Success, string? Error, Document? Document)> UploadAsync(
        string name, string? description, DateTime effectiveDate, string uploaderEmail, Stream fileStream, string fileName, string contentType, long fileSize)
    {
        if (fileSize > MaxFileSizeBytes)
            return (false, $"File exceeds the maximum size of {MaxFileSizeBytes / 1024 / 1024} MB.", null);

        if (fileSize == 0)
            return (false, "File is empty.", null);

        var storagePath = StoragePath;
        Directory.CreateDirectory(storagePath);

        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(storagePath, storedFileName);

        await using (var fileOut = File.Create(fullPath))
        {
            await fileStream.CopyToAsync(fileOut);
        }

        var document = new Document
        {
            Name = name,
            Description = description,
            FileName = fileName,
            StoragePath = storedFileName,
            UploadedByEmail = uploaderEmail,
            EffectiveDate = effectiveDate,
            FileSizeBytes = fileSize,
            ContentType = contentType
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (true, null, document);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var document = await db.Documents.FindAsync(id);
        if (document is null)
            return (false, "Document not found.");

        var fullPath = Path.Combine(StoragePath, document.StoragePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        db.Documents.Remove(document);
        await db.SaveChangesAsync();

        return (true, null);
    }

    public string? GetPhysicalPath(Document document)
    {
        var fullPath = Path.Combine(StoragePath, document.StoragePath);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
