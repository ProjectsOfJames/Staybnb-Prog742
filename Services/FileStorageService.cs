namespace Staybnb.Services;

public interface IFileStorage
{
    /// <summary>Validates and saves an upload. Returns the stored file name. Throws InvalidOperationException with a user-friendly message if invalid.</summary>
    Task<string> SaveAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes);

    string GetPhysicalPath(string folder, string fileName);
}

/// <summary>
/// Saves uploads OUTSIDE wwwroot (App_Data/uploads/...), so private files such as IDs and passports
/// are never reachable by guessing a URL. They are served only through authorised controller actions.
/// </summary>
public class FileStorageService : IFileStorage
{
    private readonly string _root;

    public FileStorageService(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
    }

    public async Task<string> SaveAsync(IFormFile file, string folder, string[] allowedExtensions, long maxBytes)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Please choose a file to upload.");

        if (file.Length > maxBytes)
            throw new InvalidOperationException($"The file is too large (maximum {maxBytes / 1024 / 1024} MB).");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            throw new InvalidOperationException($"Only these file types are allowed: {string.Join(", ", allowedExtensions)}.");

        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);

        // Never use the client's file name: a random name prevents overwrites and path tricks.
        var storedName = $"{Guid.NewGuid():N}{ext}";
        await using var stream = File.Create(Path.Combine(dir, storedName));
        await file.CopyToAsync(stream);

        return storedName;
    }

    public string GetPhysicalPath(string folder, string fileName)
    {
        // GetFileName strips any directory part, blocking "../" traversal
        return Path.Combine(_root, folder, Path.GetFileName(fileName));
    }
}