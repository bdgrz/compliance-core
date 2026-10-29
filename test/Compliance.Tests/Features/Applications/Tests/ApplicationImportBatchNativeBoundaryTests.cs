using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationImportBatchNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedBatchAuthorityGivenManyCorrectionsOfOneStagedRow()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        for (var index = 0; index < 48; index++)
        {
            var corrected = await host.CorrelateAsync(batch.Id, row.RowId, index + 1, "create_new");
            Assert.True(corrected.IsSuccess, corrected.Error?.Message);
        }

        // Act
        var owner = await host.BatchAsync(batch.Id);
        var retained = typeof(ApplicationImportBatch).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(49, owner.Revision);
        Assert.NotNull((await host.BatchHistoryAsync(owner)).GetCorrelation(row.RowId));
        Assert.Null(owner.Plan);
    }
}
