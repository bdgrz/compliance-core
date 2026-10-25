using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportSourceNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedSourceAuthorityGivenManyBatchesSettledBeforeAdmission()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        for (var index = 0; index < 48; index++)
        {
            var batch = await host.StageBatchAsync(key: "many-fences");
            await host.SealPlanAsync(batch.Id, [ApplicationImportHost.Create(Uuid.CreateVersion4(), $"record-{index}")]);
            Assert.True((await host.CancelAsync(batch.Id, 2)).IsSuccess);
            await host.ReactCancellationAsync(batch.Id);
        }

        // Act
        var source = await host.SourceAsync("many-fences");
        var retained = typeof(ApplicationImportSource).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(source) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Null(source.ActiveBatchId);
        Assert.Equal(48, source.Revision);
    }
}
