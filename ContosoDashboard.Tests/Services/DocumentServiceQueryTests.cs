using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class DocumentServiceQueryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsOnlyAuthorizedDocuments()
    {
        await using var context = SeedContext();
        context.Documents.AddRange(
            new Document { DocumentId = 1, Title = "Project guide", Category = DocumentCategories.Other, OriginalFileName = "guide.pdf", FilePath = "2/personal/a.pdf", FileType = "application/pdf", FileSize = 10, UploadedByUserId = 1, ProjectId = 1 },
            new Document { DocumentId = 2, Title = "Private", Category = DocumentCategories.Other, OriginalFileName = "private.pdf", FilePath = "1/personal/b.pdf", FileType = "application/pdf", FileSize = 10, UploadedByUserId = 1 });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var memberResults = await service.SearchAsync(new DocumentQuery { Search = "guide" }, 2);
        var outsiderResults = await service.SearchAsync(new DocumentQuery { }, 5);

        Assert.Single(memberResults.Items);
        Assert.Empty(outsiderResults.Items);
    }

    [Fact]
    public async Task ShareAsync_GrantsAccessAndCreatesAuditActivity()
    {
        await using var context = SeedContext();
        context.Documents.Add(new Document { DocumentId = 1, Title = "Personal", Category = DocumentCategories.PersonalFiles, OriginalFileName = "a.pdf", FilePath = "1/personal/a.pdf", FileType = "application/pdf", FileSize = 10, UploadedByUserId = 1 });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.ShareAsync(1, 1, recipientUserId: 2);

        Assert.True(await service.CanAccessAsync(1, 2));
        Assert.Equal(DocumentActivityActions.Share, await context.DocumentActivities.Select(a => a.Action).SingleAsync());
    }

    private static ApplicationDbContext SeedContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new ApplicationDbContext(options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner", DisplayName = "Owner" }, new User { UserId = 2, Email = "member", DisplayName = "Member" }, new User { UserId = 4, Email = "admin", DisplayName = "Admin", Role = UserRole.Administrator }, new User { UserId = 5, Email = "outsider", DisplayName = "Outsider" });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Project", ProjectManagerId = 3 });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 2 });
        context.SaveChanges();
        return context;
    }

    private static DocumentService CreateService(ApplicationDbContext context) => new(context, new FileStorageService(Options.Create(new DocumentStorageOptions { UploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), AllowedExtensions = [".pdf"], AllowedContentTypes = ["application/pdf"] }), new TestEnvironment()), new SafeScanner(), new DocumentAuditService(context), Options.Create(new DocumentStorageOptions { AllowedExtensions = [".pdf"], AllowedContentTypes = ["application/pdf"] }), new NotificationService(context));
    private sealed class SafeScanner : IFileScanService { public Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(new FileScanResult(FileScanStatus.Safe)); }
    private sealed class TestEnvironment : IWebHostEnvironment { public string ApplicationName { get; set; } = "Tests"; public string EnvironmentName { get; set; } = "Development"; public string WebRootPath { get; set; } = Path.GetTempPath(); public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!; public string ContentRootPath { get; set; } = Path.GetTempPath(); public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!; }
}
