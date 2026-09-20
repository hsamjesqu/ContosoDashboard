using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuditServiceTests
{
    [Fact]
    public async Task AdministratorReportAggregatesRetainedActivityAndDeniesEmployees()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(new User { UserId = 1, Email = "employee", DisplayName = "Employee" }, new User { UserId = 4, Email = "admin", DisplayName = "Admin", Role = UserRole.Administrator });
        await context.SaveChangesAsync();
        var audit = new DocumentAuditService(context);
        await audit.RecordAsync(null, 1, DocumentActivityActions.Delete, "retained");

        var report = await audit.GetReportAsync(4);
        Assert.Equal(1, report.AccessPatterns[DocumentActivityActions.Delete]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => audit.GetReportAsync(1));
        Assert.Single(await audit.GetRetainedActivityAsync(4));
    }
}
