namespace Staybnb.Services;

public interface IFileStorage
{
    /// <summary>Returns a user-friendly error message, or null when the file is acceptable.</summary>
    string? Validate(IFormFile? file, string[] allowedExtensions, long maxBytes);

    /// <summary>Saves a PRIVATE file (outside wwwroot). Returns the stored file name. Throws InvalidOperationException if invalid.</summary>
    Task<string> SaveAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes);

    /// <summary>Saves a PUBLIC file under wwwroot/uploads/{folder}. Returns its URL, e.g. /uploads/properties/abc.jpg.</summary>
    Task<string> SavePublicAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes);

    /// <summary>Removes a file previously saved with SavePublicAsync (used to clean up after a failed save).</summary>
    void DeletePublic(string url);

    string GetPhysicalPath(string folder, string fileName);
}

/// <summary>
/// Private files (IDs, passports) go to App_Data/uploads and are only served through authorised actions.
/// Public files (property photos) go to wwwroot/uploads so the gallery can display them.
/// </summary>
public class FileStorageService : IFileStorage
{
    private readonly string _privateRoot;
    private readonly string _publicRoot;

    public FileStorageService(IWebHostEnvironment env)
    {
        _privateRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _publicRoot = Path.Combine(webRoot, "uploads");
    }

    public string? Validate(IFormFile? file, string[] allowedExtensions, long maxBytes)
    {
        if (file == null || file.Length == 0)
            return "Please choose a file to upload.";

        if (file.Length > maxBytes)
            return $"The file is too large (maximum {maxBytes / 1024 / 1024} MB).";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return $"Only these file types are allowed: {string.Join(", ", allowedExtensions)}.";

        return null;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes)
    {
        return await WriteAsync(file, Path.Combine(_privateRoot, folder), allowedExtensions, maxBytes);
    }

    public async Task<string> SavePublicAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes)
    {
        var name = await WriteAsync(file, Path.Combine(_publicRoot, folder), allowedExtensions, maxBytes);
        return $"/uploads/{folder}/{name}";
    }

    public void DeletePublic(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;

        var relative = url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_publicRoot, relative));

        // Only ever delete inside the uploads folder
        if (!full.StartsWith(Path.GetFullPath(_publicRoot), StringComparison.OrdinalIgnoreCase)) return;

        if (File.Exists(full)) File.Delete(full);
    }

    public string GetPhysicalPath(string folder, string fileName)
    {
        // GetFileName strips any directory part, blocking "../" traversal
        return Path.Combine(_privateRoot, folder, Path.GetFileName(fileName));
    }

    private async Task<string> WriteAsync(IFormFile file, string directory, string[] allowedExtensions, long maxBytes)
    {
        var error = Validate(file, allowedExtensions, maxBytes);
        if (error != null) throw new InvalidOperationException(error);

        Directory.CreateDirectory(directory);

        // Never use the client's file name: a random name prevents overwrites and path tricks.
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(directory, storedName));
        await file.CopyToAsync(stream);

        return storedName;
    }
}