using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramNativeProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRollBackWholeProjectionBatchGivenMissingOrUnboundAuthoredValue(bool missing)
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var content = new FitzKvImmutableContentStore(kv);
        var tenant = Uuid.CreateVersion4();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var first = Assert.IsType<ProgramCreated>(await ProgramSeeds.CreatedAsync(content, tenant, firstId, "First",
            Plan(), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow));
        var second = Assert.IsType<ProgramCreated>(await ProgramSeeds.CreatedAsync(content, tenant, secondId, "Second",
            Plan(), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow));
        var wrong = missing ? second.Content with { ContentId = Uuid.CreateVersion4() } :
            await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), ProgramContent.Schema, new ProgramContent("Another value", Plan()));
        var invalid = second with { Content = wrong };
        var directory = new FitzProgramDirectory(kv, content);
        var identity = new CheckpointIdentity("ProgramDirectory", EventStreamPattern.ForPattern(tenant.ToString()));
        Exception? failure;

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            failure = await Record.ExceptionAsync(() => directory.ApplyAsync(invalid).AsTask());
        }
        var firstRow = await directory.GetAsync(tenant, firstId);
        var secondRow = await directory.GetAsync(tenant, secondId);
        var revisions = await directory.ListRevisionsAsync(tenant, firstId, 200, null);
        var checkpoint = await directory.LoadCheckpointAsync(tenant);

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(missing, failure is ImmutableContentUnavailableException);
        Assert.Null(firstRow);
        Assert.Null(secondRow);
        Assert.Null(revisions);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
    }

    static ProgramPlan Plan() => new(null, null, null, null, null, null);
}
