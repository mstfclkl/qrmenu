using Microsoft.AspNetCore.Http;

namespace QrMenu.Web.Services;

public static class FileValidator
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, byte[]> Signatures = new()
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } },
        { "image/gif",  new byte[] { 0x47, 0x49, 0x46, 0x38 } }
    };

    public static (bool Valid, string? Error) Validate(IFormFile file)
    {
        if (file == null || file.Length == 0) return (false, "No file provided.");
        if (file.Length > MaxFileSizeBytes) return (false, "File exceeds maximum size of 5 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(ext))
            return (false, "Only JPG, PNG, WebP and GIF images are allowed.");
        using var stream = file.OpenReadStream();
        var header = new byte[8];
        stream.Read(header, 0, header.Length);
        var matched = Signatures.Any(sig => header.Take(sig.Value.Length).SequenceEqual(sig.Value));
        if (!matched) return (false, "File content does not match an allowed image format.");
        return (true, null);
    }
}
