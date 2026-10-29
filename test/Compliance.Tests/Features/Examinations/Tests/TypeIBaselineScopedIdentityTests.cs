using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Examinations.Queries;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class TypeIBaselineScopedIdentityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldPreserveBothOwnerLineagesGivenReusedBaselineAndAmendmentIds(bool differentPrograms, bool amend)
    {
        // Arrange
        await using var source = await Sources.CreateAsync(differentPrograms, amend);
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), source.Content);

        // Act
        await source.ProjectAsync(directory);

        // Assert
        foreach (var owner in source.Owners)
        {
            var current = await directory.GetAsync(source.Tenant, owner.Id);
            Assert.Equal(JsonSerializer.Serialize(owner.Current, ComplianceCoreJsonContext.Default.ExaminationEngagementView),
                JsonSerializer.Serialize(current, ComplianceCoreJsonContext.Default.ExaminationEngagementView));
            Assert.Equal("baseline_frozen", current!.Status);
            Assert.Equal(amend ? 2 : 1, owner.Baseline!.Revision);
        }
        Assert.Equal(source.SharedBaseline, source.Owners[0].BaselineHistory[0].TypeIBaselineId);
        Assert.Equal(source.SharedBaseline, source.Owners[1].BaselineHistory[0].TypeIBaselineId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResolveOnlyUniqueProgramOwnerGivenExistingScopedBaselineQuery(bool differentPrograms)
    {
        // Arrange
        await using var source = await Sources.CreateAsync(differentPrograms, true);
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), source.Content);
        await source.ProjectAsync(directory);
        var handler = new GetTypeIBaselineHandler(directory, source.Events, source.Examinations, source.Content);

        // Act
        foreach (var owner in source.Owners)
        {
            var baseline = owner.Baseline!;
            var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<GetTypeIBaseline>(
                new GetTypeIBaseline(source.Tenant, baseline.ProgramId, source.SharedAmendment),
                new RequestDispatchContext(RequestActor.System)), CancellationToken.None);

            // Assert
            if (differentPrograms)
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(owner.Id, result.Value.ExaminationEngagementId);
                AssertSameBaseline(baseline, result.Value);
            }
            else
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
                Assert.False(result.Error.IsTransient);
            }
            var original = await directory.GetBaselineAsync(source.Tenant, owner.Id, source.SharedBaseline);
            var amended = await directory.GetBaselineAsync(source.Tenant, owner.Id, source.SharedAmendment);
            AssertSameBaseline(owner.BaselineHistory[0], original!);
            AssertSameBaseline(baseline, amended!);
            Assert.Equal(source.SharedBaseline, amended!.AmendsBaselineId);
            Assert.Equal(owner.BaselineHistory[0].CanonicalManifest, original!.CanonicalManifest);
            Assert.Equal(owner.BaselineHistory[0].ContentSha256, original.ContentSha256);
            Assert.Equal(owner.BaselineHistory[0].SourceContentManifest, original.SourceContentManifest);
        }
        Assert.Null(await directory.GetBaselineAsync(source.Tenant, Uuid.CreateVersion4(), source.SharedBaseline));
        Assert.Null(await directory.GetBaselineAsync(Uuid.CreateVersion4(), source.Owners[0].Id, source.SharedBaseline));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseUniqueLookupGivenBaselineSourcePendingBeforeOrDuringRead(bool appendDuringLookup)
    {
        // Arrange
        await using var source = await Sources.CreateAsync(true, false);
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), source.Content);
        await source.ProjectAsync(directory);
        var extra = new ExaminationEngagementRules(source.Tenant, source.Program, Uuid.CreateVersion4());
        var boundary = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        Assert.Null(extra.Create(new ExaminationEngagementContent("type_i", "Another audit", new DateOnly(2026, 9, 30), null,
            ["security"], boundary, []), actor, "Examiner", at));
        var preview = TypeIBaselinePreviewTestFactory.Create(source.Tenant, source.Program, extra.Id,
            [new TypeIBaselineSourceReference("approved_boundary", boundary, Uuid.CreateVersion4(), 1, new string('b', 64))]);
        Assert.Null(extra.FreezeBaseline(1, source.SharedBaseline, preview, "Confirmed", actor, "Examiner", at.AddMinutes(1)));
        async Task AppendAsync()
        {
            await source.SaveAsync(extra);
        }
        if (!appendDuringLookup)
            await AppendAsync();
        IExaminationEngagementDirectoryReader selected = appendDuringLookup
            ? new ChangingDirectory(directory, AppendAsync) : directory;
        var handler = new GetTypeIBaselineHandler(selected, source.Events, source.Examinations, source.Content);

        // Act
        var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<GetTypeIBaseline>(
            new GetTypeIBaseline(source.Tenant, source.Program, source.SharedBaseline),
            new RequestDispatchContext(RequestActor.System)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldIgnoreOtherTenantExaminationActivityGivenUniqueScopedBaselineQuery()
    {
        // Arrange
        await using var source = await Sources.CreateAsync(true, false);
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), source.Content);
        await source.ProjectAsync(directory);
        var foreign = new ExaminationEngagementRules(Uuid.CreateVersion4(), source.Program, Uuid.CreateVersion4());
        Assert.Null(foreign.Create(new ExaminationEngagementContent("type_i", "Other tenant audit", new DateOnly(2026, 9, 30),
            null, ["security"], Uuid.CreateVersion4(), []), Uuid.CreateVersion4(), "Examiner", DateTimeOffset.UtcNow));
        await source.SaveAsync(foreign);
        var handler = new GetTypeIBaselineHandler(directory, source.Events, source.Examinations, source.Content);

        // Act
        var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<GetTypeIBaseline>(
            new GetTypeIBaseline(source.Tenant, source.Program, source.SharedBaseline),
            new RequestDispatchContext(RequestActor.System)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(source.Owners[0].Id, result.Value.ExaminationEngagementId);
    }

    [Fact]
    public async Task ShouldRequireReplayGivenOldUnscopedGenerationCheckpoint()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenant = Uuid.CreateVersion4();
        var legacy = new LegacyDirectory(client);
        var oldIdentity = new CheckpointIdentity("ExaminationEngagementDirectoryV2", EventStreamPattern.ForPattern(tenant.ToString()));
        var oldCheckpoint = new ProjectionCheckpoint(new EventCursor("old-flat-baseline-rows"));
        await using (var batch = await legacy.BeginAsync(new ProjectionBatchContext(oldIdentity, ProjectionCheckpoint.Start)))
            await batch.CommitAsync(oldCheckpoint);
        var current = new FitzExaminationEngagementDirectory(client, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new InMemoryKvClient()));

        // Act
        var actual = await current.LoadCheckpointAsync(tenant);

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, actual);
        Assert.Equal(oldCheckpoint, await legacy.LoadCheckpointAsync(oldIdentity));
    }

    static void AssertSameBaseline(TypeIBaselineView expected, TypeIBaselineView actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected, ComplianceCoreJsonContext.Default.TypeIBaselineView),
            JsonSerializer.Serialize(actual, ComplianceCoreJsonContext.Default.TypeIBaselineView));

    sealed class ChangingDirectory(IExaminationEngagementDirectoryReader inner, Func<Task> append)
        : IExaminationEngagementDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);
        public ValueTask<ExaminationEngagementView?> GetAsync(Uuid tenantId, Uuid examinationEngagementId, CancellationToken ct = default) =>
            inner.GetAsync(tenantId, examinationEngagementId, ct);
        public ValueTask<TypeIBaselineView?> GetBaselineAsync(Uuid tenantId, Uuid examinationEngagementId, Uuid typeIBaselineId, CancellationToken ct = default) =>
            inner.GetBaselineAsync(tenantId, examinationEngagementId, typeIBaselineId, ct);
        public async ValueTask<IReadOnlyList<TypeIBaselineView>> FindBaselinesAsync(Uuid tenantId, Uuid programId, Uuid typeIBaselineId,
            CancellationToken ct = default)
        {
            var found = await inner.FindBaselinesAsync(tenantId, programId, typeIBaselineId, ct);
            await append();
            return found;
        }
    }

    sealed class LegacyDirectory(Cntryl.Fitz.IKvClient client)
        : FitzKvProjectionStore(client, "kv://bdgrz/examination-engagement-directory/projection-v2", "ExaminationEngagementDirectoryV2")
    { }

    [Fact]
    public async Task ShouldRejectConflictingReplayGivenOnlyFrozenSourceProofDescriptorChanged()
    {
        // Arrange
        await using var source = await Sources.CreateAsync(false, false, 1);
        var directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), source.Content);
        await source.ProjectAsync(directory);
        var owner = source.Owners[0];
        var changed = owner.Baseline! with
        {
            SourceContentManifest = new ImmutableContentRef(Uuid.CreateVersion4(), new string('d', 64), 100,
                "application/json", "snapshot-source-publication-manifest.v1"),
        };
        var identity = new CheckpointIdentity(FitzExaminationEngagementDirectory.ProjectorName,
            EventStreamPattern.ForPattern(source.Tenant.ToString(), "examinations"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            await directory.LoadCheckpointAsync(source.Tenant)));

        // Act
        var error = await Record.ExceptionAsync(async () =>
            await directory.ApplyAsync(await ExaminationScenario.ReplaceStateAsync(source.Content,
                (await source.FactsAsync(owner)).Single(fact => fact is TypeIBaselineFrozen),
                new ExaminationEngagementState(owner.Current!, changed))));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    sealed class Sources : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid SharedBaseline { get; } = Uuid.CreateVersion4();
        public Uuid SharedAmendment { get; } = Uuid.CreateVersion4();
        readonly ExaminationScenario _scenario = new();
        public InMemoryEventStore Events => _scenario.Events;
        public Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore Content => _scenario.Content;
        public ExaminationEngagementReader Examinations => _scenario.Examinations;
        public List<ExaminationEngagementRules> Owners { get; } = [];
        public Task SaveAsync(ExaminationEngagementRules rules) => _scenario.SaveAsync(rules);
        public Task<IReadOnlyList<DomainEvent>> FactsAsync(ExaminationEngagementRules rules) => _scenario.FactsAsync(rules);
        public ValueTask DisposeAsync() => _scenario.DisposeAsync();

        public static async Task<Sources> CreateAsync(bool differentPrograms, bool amend, int ownerCount = 2)
        {
            var source = new Sources();
            var at = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
            for (var index = 0; index < ownerCount; index++)
            {
                var program = differentPrograms && index == 1 ? Uuid.CreateVersion4() : source.Program;
                var owner = new ExaminationEngagementRules(source.Tenant, program, Uuid.CreateVersion4());
                var boundary = Uuid.CreateVersion4();
                var actor = Uuid.CreateVersion4();
                Assert.Null(owner.Create(new ExaminationEngagementContent("type_i", "Audit LLP", new DateOnly(2026, 9, 30),
                    null, ["security"], boundary, []), actor, "Examiner", at));
                var preview = TypeIBaselinePreviewTestFactory.Create(source.Tenant, program, owner.Id,
                    [new TypeIBaselineSourceReference("approved_boundary", boundary, Uuid.CreateVersion4(), 1, new string('b', 64))]);
                Assert.Null(owner.FreezeBaseline(1, source.SharedBaseline, preview, "Confirmed exact source", actor,
                    "Examiner", at.AddMinutes(1)));
                if (amend)
                {
                    var changed = TypeIBaselinePreviewTestFactory.Create(source.Tenant, program, owner.Id,
                        [new TypeIBaselineSourceReference("approved_boundary", boundary, Uuid.CreateVersion4(), 2, new string('c', 64))]);
                    Assert.Null(owner.AmendBaseline(2, source.SharedAmendment, changed, [boundary], [], "Corrected source scope",
                        Uuid.CreateVersion4(), "Approver", at.AddMinutes(2)));
                }
                source.Owners.Add(owner);
                await source.SaveAsync(owner);
            }
            return source;
        }

        public async Task ProjectAsync(FitzExaminationEngagementDirectory directory)
        {
            var pattern = EventStreamPattern.ForPattern(Tenant.ToString(), "examinations");
            var identity = new CheckpointIdentity(FitzExaminationEngagementDirectory.ProjectorName, pattern);
            var before = await directory.LoadCheckpointAsync(Tenant);
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, before));
            var cursor = EventCursor.Start;
            await foreach (var record in Events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
    }
}
