namespace ContosoDashboard.Services;

public enum FileScanStatus { Safe, Rejected, Unavailable }
public sealed record FileScanResult(FileScanStatus Status, string? Reason = null);

public interface IFileScanService
{
    Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public sealed class TrainingFileScanService : IFileScanService
{
    public Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        if (content == null || !content.CanRead || content.Length == 0)
            return Task.FromResult(new FileScanResult(FileScanStatus.Unavailable, "The scanner could not inspect the file."));
        return Task.FromResult(new FileScanResult(FileScanStatus.Safe));
    }
}