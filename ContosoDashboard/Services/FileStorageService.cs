using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class DocumentStorageOptions
{
    public string UploadRoot { get; set; } = "AppData/uploads";
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [];
    public string[] AllowedContentTypes { get; set; } = [];
}

public sealed record StoredFile(string RelativePath, long Length);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string userId, int? projectId, string extension, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    string GetAbsolutePath(string relativePath);
}

public sealed class FileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly DocumentStorageOptions _options;

    public FileStorageService(IOptions<DocumentStorageOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(_options.UploadRoot)
            ? _options.UploadRoot
            : Path.Combine(environment.ContentRootPath, _options.UploadRoot));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string userId, int? projectId, string extension, CancellationToken cancellationToken = default)
    {
        if (content == null || !content.CanRead) throw new InvalidOperationException("The file stream is unavailable.");
        if (content.Length > _options.MaxFileSizeBytes) throw new InvalidDataException("The file exceeds the configured size limit.");

        var safeExtension = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        var folder = Path.Combine(userId, projectId?.ToString() ?? "personal");
        var relative = Path.Combine(folder, $"{Guid.NewGuid():N}{safeExtension}").Replace('\\', '/');
        var absolute = GetAbsolutePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await using var target = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);
        return new StoredFile(relative, target.Length);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = GetAbsolutePath(relativePath);
        if (File.Exists(absolute)) File.Delete(absolute);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = GetAbsolutePath(relativePath);
        Stream? stream = File.Exists(absolute)
            ? new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("The storage path is invalid.");
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The storage path is outside the upload root.");
        return fullPath;
    }
}