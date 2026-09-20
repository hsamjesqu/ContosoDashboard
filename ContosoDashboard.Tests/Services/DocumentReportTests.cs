using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class DocumentReportTests
{
    [Fact]
    public async Task ReportsAreRestrictedToAdministrators()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(
            new User { UserId = 1, Email = "admin", DisplayName = "Admin", Role = UserRole.Administrator },
            new User { UserId = 2, Email = "employee", DisplayName = "Employee", Role = UserRole.Employee });
        await context.SaveChangesAsync();

        var service = new DocumentAuditService(context);
        await service.RecordAsync(null, 2, DocumentActivityActions.Upload, "report.pdf");

        var report = await service.GetReportAsync(1);
        Assert.Equal(1, report.AccessPatterns[DocumentActivityActions.Upload]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetReportAsync(2));
    }
}