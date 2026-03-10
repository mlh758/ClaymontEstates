using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public record DocumentUploadParams(
    string Name,
    string? Description,
    DateTime EffectiveDate,
    string UploaderEmail,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize);

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

    public async Task<(bool Success, string? Error, Document? Document)> UploadAsync(DocumentUploadParams p)
    {
        if (p.FileSize > MaxFileSizeBytes)
            return (false, $"File exceeds the maximum size of {MaxFileSizeBytes / 1024 / 1024} MB.", null);

        if (p.FileSize == 0)
            return (false, "File is empty.", null);

        var storagePath = StoragePath;
        Directory.CreateDirectory(storagePath);

        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(p.FileName)}";
        var fullPath = Path.Combine(storagePath, storedFileName);

        await using (var fileOut = File.Create(fullPath))
        {
            await p.FileStream.CopyToAsync(fileOut);
        }

        var document = new Document
        {
            Name = p.Name,
            Description = p.Description,
            FileName = p.FileName,
            StoragePath = storedFileName,
            UploadedByEmail = p.UploaderEmail,
            EffectiveDate = p.EffectiveDate,
            FileSizeBytes = p.FileSize,
            ContentType = p.ContentType
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
