using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ReviewedSystemTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid InstanceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid RequesterId = Uuid.CreateVersion4();
    static readonly DateTimeOffset DayOne = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureReviewedSystemRevisionAtTypeIDateGivenLaterRevision()
    {
        // Arrange
        var registration = Registered(DayOne);
        var revision = Revised(DayOne.AddDays(35));
        var fixture = await Fixture([registration, revision]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("reviewed_system_revision", source.Kind);
        Assert.Equal(InstanceId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(InstanceId, "reviewed_system_revision_v1:1"),
            source.VersionId);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldCaptureScheduledRetirementGivenEffectiveDateAfterTypeIDate()
    {
        // Arrange
        var registration = Registered(DayOne);
        var retirement = new SystemInstanceRetired(TenantId, ApplicationId, InstanceId, 2,
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero), "Migration planned",
            AuthorId, "Examiner", DayOne.AddDays(10), ApplicationFacts.SystemInstance(TenantId, InstanceId), null);
        var fixture = await Fixture([registration, retirement]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["reviewed_system_revision", "reviewed_system_retirement"],
            result.Value.Sources.Select(static source => source.Kind));
        Assert.Equal(1, result.Value.Sources[0].Revision);
        Assert.Equal(2, result.Value.Sources[1].Revision);
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("scheduled after the Type I date", StringComparison.Ordinal));
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldBlockReviewedSystemGivenParentApplicationWasNotCaptured()
    {
        // Arrange
        var fixture = await Fixture([Registered(DayOne)], includeParentApplication: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("parent application", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldForbidRestrictedReviewedSystemGivenRequesterHasNoPermission()
    {
        // Arrange
        var fixture = await Fixture([Registered(DayOne)], restrictedApplication: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain(ApplicationId.ToString(), result.Error?.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldBlockReviewedSystemGivenRequiredReferencesAreMissing()
    {
        // Arrange
        var registration = Registered(DayOne) with
        {
            AccessBoundaryReference = null,
            SourceIdentifier = null,
        };
        var fixture = await Fixture([registration]);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("no access boundary", StringComparison.Ordinal));
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("no source identifier", StringComparison.Ordinal));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("access_boundary_missing", StringComparison.Ordinal));
        Assert.Contains(result.Value.ScopeDifferences,
            difference => difference.Contains("source_identifier_missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldBlockReviewedSystemGivenProjectedIdentityConflict()
    {
        // Arrange
        var fixture = await Fixture([Registered(DayOne)], declarationConflict: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers,
            blocker => blocker.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldCaptureLegacyReviewedSystemGivenParentApplicationDeclaration()
    {
        // Arrange
        var fixture = await Fixture([], legacy: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), fixture.Context,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("reviewed_system_revision", source.Kind);
        Assert.Equal(1, source.Revision);
        Assert.Matches("^[0-9a-f]{64}$", source.ContentSha256);
        Assert.Empty(result.Value.Blockers);
    }

    static async Task<FixtureState> Fixture(IReadOnlyList<DomainEvent> instanceEvents,
        bool includeParentApplication = true, bool restrictedApplication = false,
        bool declarationConflict = false, bool legacy = false)
    {
        var events = new InMemoryEventStore();
        var applicationEvent = DomainEventSeed.Attach(new ApplicationDeclared(TenantId,
            ApplicationId, "Payroll", "Run payroll", null, AuthorId, "Examiner", DayOne, ApplicationFacts.Application(TenantId, ApplicationId), null,
            IsRestricted: restrictedApplication),
            ApplicationId, 1);
        var legacyDeclaration = legacy
            ? new SystemInstanceDeclared(TenantId, ApplicationId, InstanceId, 2,
                "Production", "production", "Boundary v1", "payroll-prod", AuthorId,
                "Examiner", DayOne)
            : null;
        var attachedLegacyDeclaration = legacyDeclaration is null ? null :
            DomainEventSeed.Attach(legacyDeclaration, ApplicationId, 2);
        DomainEvent[] attachedApplicationEvents = attachedLegacyDeclaration is null
            ? [applicationEvent]
            : [applicationEvent, attachedLegacyDeclaration];
        await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "applications",
            ApplicationId.ToString()), 0, attachedApplicationEvents);
        var attachedInstanceEvents = instanceEvents.Select((domainEvent, index) =>
            DomainEventSeed.Attach(domainEvent, InstanceId, (ulong)index + 1)).ToArray();
        if (attachedInstanceEvents.Length > 0)
            await events.AppendAsync(new EventStreamAddress(TenantId.ToString(), "system-instances",
                InstanceId.ToString()), 0, attachedInstanceEvents);
        var checkpoint = await CheckpointAfterAsync(events);

        var application = attachedLegacyDeclaration is null
            ? DeclareApplication(restrictedApplication)
            : new AggregateScenario<DeclaredApplication>(
                new DeclaredApplication(TenantId, ApplicationId))
                .Given(attachedApplicationEvents).Aggregate;
        var instance = legacy
            ? new DeclaredSystemInstance(TenantId, InstanceId)
            : new AggregateScenario<DeclaredSystemInstance>(
                new DeclaredSystemInstance(TenantId, InstanceId)).Given(attachedInstanceEvents).Aggregate;
        var latestState = instanceEvents.OfType<SystemInstanceRevised>().LastOrDefault();
        var registered = legacy ? null : instanceEvents.OfType<SystemInstanceRegistered>().Single();
        var retired = instanceEvents.OfType<SystemInstanceRetired>().SingleOrDefault();
        var name = latestState?.Name ?? legacyDeclaration?.Name ?? registered!.Name;
        var kind = latestState?.Kind ?? legacyDeclaration?.Kind ?? registered!.Kind;
        var accessBoundary = latestState?.AccessBoundaryReference ??
            legacyDeclaration?.AccessBoundaryReference ?? registered!.AccessBoundaryReference;
        var sourceIdentifier = latestState?.SourceIdentifier ??
            legacyDeclaration?.SourceIdentifier ?? registered!.SourceIdentifier;
        var declaredBy = legacyDeclaration?.ActorMemberId ?? registered?.ActorMemberId ?? AuthorId;
        var declaredByDisplay = legacyDeclaration?.ActorDisplay ?? registered?.ActorDisplay ?? "Examiner";
        var declaredAt = legacyDeclaration?.ChangedAt ?? registered?.ChangedAt ?? DayOne;
        var unresolved = InstanceGaps(accessBoundary, sourceIdentifier).ToList();
        if (declarationConflict)
            unresolved.Add("declaration_conflict");
        var instanceView = new SystemInstanceView(TenantId, ApplicationId, InstanceId, name, kind,
            accessBoundary, "manual", sourceIdentifier, unresolved,
            declaredBy, declaredByDisplay, declaredAt)
        {
            Revision = legacy ? 1 : instance.Revision,
            LegacyApplicationRevision = legacy ? legacyDeclaration!.ApplicationRevision : null,
            Lifecycle = retired is null ? "active" : "retired",
            Retirement = retired is null ? null : new RetirementView(retired.EffectiveAt,
                retired.Reason, null),
        };
        var applicationView = new ApplicationView(TenantId, ApplicationId,
            legacy ? legacyDeclaration!.ApplicationRevision : 1, "Payroll",
            "Run payroll", null, "manual", ApplicationId.ToString(), true, [], AuthorId,
            "Examiner", DayOne);
        var directory = new ApplicationDirectory(applicationView, instanceView, checkpoint);
        var boundaryEntry = new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion",
            "system_instance", InstanceId.ToString(), InstanceId, string.Empty,
            "Reviewed system", false);
        var version = new BoundaryVersionView(TenantId, BoundaryId, ProgramId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("In-scope system", "type_i",
            ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
            [boundaryEntry]), "approved", new DateOnly(2026, 6, 1), AuthorId, "Examiner", DayOne);
        var boundary = new BoundaryView(TenantId, BoundaryId, ProgramId, null, version, null, 1);
        var boundaries = new BoundaryDirectory(boundary, version, checkpoint);
        var aggregates = new SourceReader(application, instance);
        var appConsistency = new SystemInstanceReadConsistency(directory, aggregates,
            new LegacySystemInstanceSource(directory, events), events);
        var boundaryConsistency = new ProgramSetupWorkReadConsistency(new ProgramDirectory(checkpoint),
            boundaries, events);
        var contributor = new ReviewedSystemTypeIBaselineSourceContributor(directory, aggregates,
            events, appConsistency, RestrictedApplicationVisibilityFixture.Create(aggregates),
            boundaries, boundaryConsistency);
        var priorSources = new List<TypeIBaselineSourceReference>
        {
            new("approved_boundary", BoundaryId, version.VersionId, version.Revision,
                SnapshotContentIdentity.ApprovedBoundaryVersion(version)),
        };
        if (includeParentApplication)
            priorSources.Add(new TypeIBaselineSourceReference("application_revision", ApplicationId,
                Uuid.CreateVersion5(ApplicationId,
                    $"application_revision_v1:{applicationView.Revision}"), applicationView.Revision,
                new string('a', 64)));
        return new FixtureState(contributor, new TypeIBaselineSourceReadContext(RequesterId,
            priorSources.AsReadOnly()));
    }

    static DeclaredApplication DeclareApplication(bool restricted)
    {
        var application = new DeclaredApplication(TenantId, ApplicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null, AuthorId,
            "Examiner", DayOne, isRestricted: restricted).IsSuccess);
        return application;
    }

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString()), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static SystemInstanceRegistered Registered(DateTimeOffset changedAt) =>
        new(TenantId, ApplicationId, InstanceId, 1, "Production", "production",
            "Boundary v1", "payroll-prod", AuthorId, "Examiner", changedAt,
            ApplicationFacts.SystemInstance(TenantId, InstanceId), null);

    static SystemInstanceRevised Revised(DateTimeOffset changedAt) =>
        new(TenantId, ApplicationId, InstanceId, 2, "Production east", "production",
            "Boundary v2", "payroll-prod-east", "Moved to the east network zone.",
            AuthorId, "Examiner", changedAt, ApplicationFacts.SystemInstance(TenantId, InstanceId), null);

    static IReadOnlyList<string> InstanceGaps(string? accessBoundary, string? sourceIdentifier) =>
    [accessBoundary is null ? "access_boundary_missing" : "access_boundary_unverified",
        sourceIdentifier is null ? "source_identifier_missing" : "source_identifier_unverified"];

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", new DateOnly(2026, 6, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        BoundaryId, [], "draft", 1, DayOne, AuthorId, "Examiner");

    sealed record FixtureState(ReviewedSystemTypeIBaselineSourceContributor Contributor,
        TypeIBaselineSourceReadContext Context);

    sealed class SourceReader(DeclaredApplication application, DeclaredSystemInstance instance)
        : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(aggregate switch
            {
                DeclaredApplication => (TAggregate)(Aggregate)application,
                DeclaredSystemInstance => (TAggregate)(Aggregate)instance,
                _ => aggregate,
            });
    }

    sealed class ApplicationDirectory(ApplicationView application, SystemInstanceView instance,
        ProjectionCheckpoint checkpoint) : IApplicationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => ValueTask.FromResult<ApplicationView?>(
            tenantId == application.TenantId && applicationId == application.ApplicationId
                ? application : null);
        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ApplicationView>([], null));
        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ApplicationRevisionView?>(null);
        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ApplicationRevisionView>?>(new Page<ApplicationRevisionView>([], null));
        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => ValueTask.FromResult<SystemInstanceView?>(
            tenantId == instance.TenantId && instanceId == instance.SystemInstanceId ? instance : null);
        public ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<SystemInstanceView>([], null));
    }

    sealed class BoundaryDirectory(BoundaryView boundary, BoundaryVersionView version,
        ProjectionCheckpoint checkpoint) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(boundary);
        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>([boundary], null));
        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(new Page<BoundaryVersionView>([version], null));
        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(version);
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);
        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(new Page<BoundaryDecisionView>([], null));
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }

    sealed class ProgramDirectory(ProjectionCheckpoint checkpoint) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => ValueTask.FromResult<ProgramView?>(null);
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<ProgramView>([], null));
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<ProgramRevisionView>?>(new Page<ProgramRevisionView>([], null));
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid programId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<ProgramRevisionView?>(null);
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);
    }
}
