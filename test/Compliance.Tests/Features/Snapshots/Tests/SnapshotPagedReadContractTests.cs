using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Snapshots.Handlers;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Queries;
using Bdgrz.Compliance.Features.Snapshots.Readers;
namespace Bdgrz.Compliance.Tests.Features.Snapshots.Tests;

public sealed class SnapshotPagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenProgramSnapshots(int limit)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new ListProgramSnapshotsHandler(new SnapshotDirectory(),
            new AggregateReader(await Program(tenantId, programId)));

        // Act
        var result = await handler.HandleAsync(Context(new ListProgramSnapshots(tenantId,
            programId, limit)), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenProgramSnapshots()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handler = new ListProgramSnapshotsHandler(new SnapshotDirectory { RejectCursor = true },
            new AggregateReader(await Program(tenantId, programId)));

        // Act
        var result = await handler.HandleAsync(Context(new ListProgramSnapshots(tenantId,
            programId, Cursor: "invalid")), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static async Task<ComplianceProgram> Program(Uuid tenantId, Uuid programId)
    {
        var program = new ProgramRules(tenantId, programId);
        Assert.Null(program.Create("Readiness", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Owner", DateTimeOffset.UtcNow));
        await using var native = new ProgramNativeFixture();
        return await ProgramSeeds.OwnerAsync(native.Content, program);
    }

    sealed class SnapshotDirectory : ISnapshotDirectoryReader
    {
        public bool RejectCursor { get; init; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(default(ProjectionCheckpoint));

        public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
            CancellationToken ct = default) => ValueTask.FromResult<SnapshotView?>(null);

        public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            RejectCursor
                ? ValueTask.FromException<Page<SnapshotView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(new Page<SnapshotView>([], null));
    }

    sealed class AggregateReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
