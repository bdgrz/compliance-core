using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Handlers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed partial class TypeIIPeriodCommandHandlerTests
{
    [Fact]
    public async Task ShouldRetainExactProgramGivenOpeningAdmittedBeforePeriodBodyExists()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var id = await fixture.AcceptOpeningAsync();
        var foreignProgram = Uuid.CreateVersion4();

        // Act
        var native = await fixture.Host.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, foreignProgram, id));
        var scoped = await fixture.Host.Periods.ReadAsync(fixture.TenantId, foreignProgram, id);

        // Assert
        Assert.Equal(fixture.ProgramId, native.ProgramId);
        Assert.Null(native.Header);
        Assert.NotNull(native.Pending);
        Assert.Null(scoped.View);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldNotAppendDecisionGivenCurrentImmutablePeriodBodyCannotBeVerified(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedStartedPeriodAsync();
        var request = await CloseRequestAsync(fixture);
        var sources = new CloseSources(fixture);
        var broken = new UnreadablePeriodState(fixture.Host.Content, unavailable);
        var periods = new TypeIIPeriodReader(fixture.Reader, fixture.Events, broken);
        var operations = new TypeIIPeriodOperations(new NativeOperationExecutor(fixture.Reader, fixture.Executor, broken),
            periods, broken, fixture.CloseClock);
        var handler = new RecordTypeIIPeriodCloseValidationHandler(operations, sources, broken);

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Equal(0, sources.Calls);
        Assert.Equal(request.ExpectedRevision, owner.Revision);
    }

    [Fact]
    public async Task ShouldFreezeOriginalUnitEnvelopeGivenNativeCloseApprovalAndExactRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RegisterActorAsync();
        await fixture.SeedCloseReadyPeriodAsync();
        var request = new ApproveTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, 2, "Reviewed exact reconciliation and acknowledged material.", [fixture.GapId]);
        var handler = fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso);
        var first = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddDays(2));

        // Act
        var retry = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var changed = await handler.HandleAsync(fixture.Context(request with { Rationale = "A different authored approval." }), CancellationToken.None);
        var owner = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId, request.CommandId));
        var accepted = Assert.IsType<TypeIIPeriodCloseApproved>(owner.Acceptance.SelectedFact);
        var descriptor = await FirstResponses.ReplayAsync(fixture.Host.Content, fixture.TenantId.ToString(), accepted.FirstResponse!);
        var result = await fixture.Host.Content.ReadJsonAsync<MutationResultContent>(fixture.TenantId.ToString(), accepted.OperationResult!);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal(3, owner.Revision);
        Assert.Equal(200, descriptor.HttpStatus);
        Assert.Equal(UnitResult.Schema, descriptor.ResultSchema);
        Assert.Equal(UnitResult.Schema, result.Schema);
        Assert.Equal(descriptor.Body, result.Value);
    }

    [Fact]
    public async Task ShouldSealNativePackageManifestImpactGivenAttributableCloseAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        var prior = await SeedClosedForAmendmentAsync(fixture);
        var current = (await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId)).View!;
        var package = await SeedClosePackageAsync(fixture, current, prior, fixture.ProgramId);
        var foreign = await SeedClosePackageAsync(fixture, current, prior, Uuid.CreateVersion4());
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddHours(1));
        var sources = new AmendmentSources(fixture);
        var request = new AmendTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, Uuid.CreateVersion4(), current.Revision, "Correct the exact changed frozen source version.");

        // Act
        var result = await AmendmentHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var native = await fixture.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, fixture.PeriodId, request.CommandId));
        var fact = Assert.IsType<TypeIIPeriodCloseAmended>(native.Acceptance.SelectedFact);
        var delta = await fixture.Host.Content.ReadJsonAsync<TypeIIPeriodMutationContent>(fixture.TenantId.ToString(), fact.Change);
        var proof = await fixture.Host.Content.ReadJsonAsync<TypeIIPeriodCloseMaterialProof>(fixture.TenantId.ToString(), delta.SourceProof!);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, saved.View!.CloseSnapshots.Count);
        var amendment = saved.View.CloseSnapshots[^1];
        Assert.Equal(prior.CloseSnapshotId, amendment.AmendsCloseSnapshotId);
        Assert.Equal(prior.ExpectedOccurrenceIds, amendment.ExpectedOccurrenceIds);
        Assert.Equal(prior.AcknowledgedMaterialItemIds, amendment.AcknowledgedMaterialItemIds);
        Assert.Equal(request.ExpectedRevision, amendment.OriginalExpectedRevision);
        Assert.Contains(prior.CloseSnapshotId, amendment.AffectedExaminationMaterialIds);
        Assert.Contains(package.AuditPackageId, amendment.AffectedExaminationMaterialIds);
        Assert.Contains(package.AuditPackageManifestId, amendment.AffectedExaminationMaterialIds);
        Assert.DoesNotContain(foreign.AuditPackageId, amendment.AffectedExaminationMaterialIds);
        Assert.Equal(request.CommandId, proof.Header.RootOperationId);
        Assert.Equal(request.PeriodId, proof.PeriodId);
        Assert.Contains(proof.SourceFacts, source => source.Fact.Stream.Resource == package.AuditPackageId.ToString());
        Assert.Equal(amendment.ChangedRecordIds, proof.ChangedRecordIds);
    }

    [Fact]
    public async Task ShouldRecoverOriginalAmendmentWithoutReselectingSourcesGivenAppendCommittedBeforeLostResponse()
    {
        // Arrange
        await using var fixture = new Fixture();
        await SeedClosedForAmendmentAsync(fixture);
        var current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var sources = new AmendmentSources(fixture);
        var request = new AmendTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.PeriodId, Uuid.CreateVersion4(), current.Revision, "Correct one exact approved source version.");
        var unknown = AmendmentHandler(fixture, sources, new CloseCommitThenTimeout(fixture.Executor));

        // Act
        await Assert.ThrowsAsync<TimeoutException>(async () => await unknown.HandleAsync(fixture.Context(request), CancellationToken.None));
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddDays(3));
        sources.Failure = new ImmutableContentUnavailableException("Unavailable after the accepted amendment.");
        var replay = await AmendmentHandler(fixture, sources).HandleAsync(fixture.Context(request), CancellationToken.None);
        var changed = await AmendmentHandler(fixture, sources).HandleAsync(fixture.Context(request with { ExpectedRevision = request.ExpectedRevision + 1 }), CancellationToken.None);
        var saved = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal(1, sources.Calls);
        Assert.Equal(request.ExpectedRevision + 1, saved.Revision);
        Assert.Equal(2, saved.View!.CloseSnapshots.Count);
    }

    static AmendTypeIIPeriodCloseHandler AmendmentHandler(Fixture fixture, ITypeIIPeriodCloseReconciliation sources,
        IAggregateExecutor? executor = null) => new(new TypeIIPeriodOperations(
            new NativeOperationExecutor(fixture.Reader, executor ?? fixture.Executor, fixture.Host.Content), fixture.Periods,
            fixture.Host.Content, fixture.CloseClock), new ManagementExecutiveAuthority(fixture.Reader,
            new ExecutivePermissions(RbacPermissions.ManagementExecutiveCiso)), sources,
            new TypeIIPeriodCloseMaterialReader(fixture.Events, fixture.Host.Content, new SystemDescriptionReader(fixture.Reader, fixture.Events, fixture.Host.Content),
                new RecordedSourceReceiptCutFactory(fixture.Events, fixture.Host.Services.GetRequiredService<INativeEventReceiptReader>()),
                new AuditPopulationReader(fixture.Reader, fixture.Events, fixture.Host.Content, new EventStoreSourceFactResolver(fixture.Events))), fixture.Host.Content);

    static async Task<TypeIIPeriodCloseSnapshotView> SeedClosedForAmendmentAsync(Fixture fixture)
    {
        await fixture.RegisterActorAsync();
        await fixture.SeedCoveredCloseReadyPeriodAsync();
        var current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var approved = await fixture.ApproveHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
            new ApproveTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PeriodId,
                current.Revision, "Reviewed exact close and acknowledged exception.", [fixture.GapId])), CancellationToken.None);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        fixture.CloseClock.Set(fixture.CloseClock.GetUtcNow().AddMinutes(1));
        current = await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId);
        var frozen = await fixture.FreezeHandler(RbacPermissions.ManagementExecutiveCiso).HandleAsync(fixture.Context(
            new FreezeTypeIIPeriodClose(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.PeriodId,
                Uuid.CreateVersion4(), current.Revision)), CancellationToken.None);
        Assert.True(frozen.IsSuccess, frozen.Error?.Message);
        return Assert.Single((await fixture.Periods.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.PeriodId)).View!.CloseSnapshots);
    }

    static async Task<AuditPackageManifestView> SeedClosePackageAsync(Fixture fixture, TypeIIPeriodView period,
        TypeIIPeriodCloseSnapshotView close, Uuid program)
    {
        var package = new AuditPackageRules(fixture.TenantId, program, Uuid.CreateVersion4());
        var source = close.SourceReferences[0];
        var input = new AuditPackageContent(period.ExaminationId, "type_ii", "type_ii_close_snapshot", close.CloseSnapshotId,
            close.ContentSha256, "Exact previous close package", [new AuditPackageSelectionEntry("sources", "frozen-source",
                source.SourceType, source.RecordId, source.Version, "json", source.ContentSha256, 2, "product_calculated", source.VersionId)], period.PeriodId);
        Assert.Null(package.Create(input, fixture.ActorMemberId, "Package recorder", fixture.CloseClock.GetUtcNow()));
        await PackageSeeds.WriteAsync(fixture.Reader, fixture.Writer, fixture.Host.Events, fixture.Host.Content, package.Current!);
        return package.Current!.Versions[^1].Manifest;
    }

    sealed class AmendmentSources(Fixture fixture) : ITypeIIPeriodCloseReconciliation
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriodReadSnapshot period,
            ClaimsPrincipal actor, Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
        {
            Calls++;
            if (Failure is { } failure)
                throw failure;
            var current = period.View!;
            var prior = current.CloseSnapshots[^1];
            var sources = prior.SourceReferences.ToArray();
            sources[0] = sources[0] with { VersionId = Uuid.CreateVersion4(), Version = sources[0].Version + 1, ContentSha256 = new string('f', 64) };
            var publication = new SnapshotSourcePublicationManifest(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
                sources.Select(source => new SnapshotSourcePublicationEntry(source.SourceType, source.RecordId, source.VersionId,
                    source.Version, source.ContentSha256, new SnapshotSourceBlob(new string('d', 64), 2, "application/json", "json", "Exact test source."))).ToArray())
            {
                RecordedThrough = operation.AcceptedAt,
                ClosePeriodId = current.PeriodId,
                CloseOperationId = operation.RootOperationId,
                SourceCoverage = TypeIBaselineSourceFamilies.Required.Select(family => TypeIBaselineContentIdentity.Coverage(family, [])).ToArray(),
            };
            var reference = await fixture.Host.Content.WriteJsonAsync(fixture.TenantId.ToString(), Uuid.CreateVersion4(), SnapshotSourcePublicationManifest.Schema, publication, ct);
            return Result<TypeIIPeriodCloseValidationContent>.Success(new TypeIIPeriodCloseValidationContent(sources,
                prior.ExpectedOccurrenceIds, prior.ExpectedOccurrenceIds, [], [], prior.AcknowledgedMaterialItemIds)
            { SourceContentManifest = reference, RecordedThrough = operation.AcceptedAt });
        }
    }

    sealed class UnreadablePeriodState(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            reference.Schema == TypeIIPeriodContent.StateSchema ? throw (unavailable
                ? new ImmutableContentUnavailableException("The exact period state is temporarily unavailable.")
                : new ImmutableContentIntegrityException("The exact period state is corrupt.")) : inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
