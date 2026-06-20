namespace Server;

public static class FileSizeExtensions
{
    /// <summary>Formats a byte count as a human-readable string (B, KB, or MB).</summary>
    public static string ToFileSize(this long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
