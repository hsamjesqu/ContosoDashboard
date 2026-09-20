using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Components;

public class TaskDetailsWorkflowTests
{
    [Fact]
    public async Task UploadingForTaskPersistsTheTaskAndProjectAssociation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.Add(new User { UserId = 1, Email = "member", DisplayName = "Member" });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Project", ProjectManagerId = 1 });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 1 });
        context.Tasks.Add(new TaskItem { TaskId = 1, Title = "Task", AssignedUserId = 1, CreatedByUserId = 1, ProjectId = 1 });
        await context.SaveChangesAsync();

        var uploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var storageOptions = Options.Create(new DocumentStorageOptions
        {
            UploadRoot = uploadRoot,
            MaxFileSizeBytes = Document.MaximumFileSize,
            AllowedExtensions = [".pdf"],
            AllowedContentTypes = ["application/pdf"]
        });
        var service = new DocumentService(
            context,
            new FileStorageService(storageOptions, new TestEnvironment()),
            new SafeScanner(),
            new DocumentAuditService(context),
            storageOptions,
            new NotificationService(context));

        await using var content = new MemoryStream("pdf"u8.ToArray());
        var document = await service.UploadAsync(new DocumentUploadRequest
        {
            Content = content,
            FileName = "task.pdf",
            ContentType = "application/pdf",
            Title = "Task attachment",
            Category = DocumentCategories.ProjectDocuments,
            ProjectId = 1,
            TaskId = 1
        }, 1);

        Assert.Equal(1, document.ProjectId);
        Assert.Equal(1, document.TaskId);
        Assert.Single(await service.GetTaskDocumentsAsync(1, 1));
        Directory.Delete(uploadRoot, recursive: true);
    }

    private sealed class SafeScanner : IFileScanService
    {
        public Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileScanResult(FileScanStatus.Safe));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}