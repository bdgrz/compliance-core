using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationEvidenceSourceReferencePreviewTests
{
    [Fact]
    public async Task ShouldIncludeEvidenceSourceGivenAffectedControlAndExactArtifactReadAccess()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var reference = EvidenceReference(tenantId, programId, controlId, artifactId);
        sourceReader.Support = Linked(reference);
        var evidenceSources = new StubEvidenceSourceReferenceDirectory([reference]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var actual = Assert.Single(result.Value.EvidenceSourceReferences);
        Assert.Equal(artifactId, actual.ArtifactId);
        Assert.Equal(controlId, actual.ControlId);
        var status = Assert.IsType<EvidenceSupportStatusView>(actual.SupportStatus);
        Assert.True(status.IsActive);
        Assert.Equal(1, status.Revision);
        Assert.DoesNotContain("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldOmitRestrictedArtifactAndKeepEvidenceContextPendingGivenMissingExactReadGrant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory(
            [EvidenceReference(tenantId, programId, controlId, artifactId)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(false), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.EvidenceSourceReferences);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepEvidenceContextPendingGivenUnmappedSupportTypeOrTruncatedPage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv, new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(kv));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory([], hasUnresolved: true,
            nextCursor: "next");
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
        Assert.Contains("evidence_source_references_over_limit", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldKeepEvidenceContextPendingGivenImportWorkerWithoutArtifactIdentity()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();
        var source = CreateApplication(tenantId, applicationId);
        var sourceReader = new SourceReader(source);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, applicationId);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient(), new Bdgrz.Compliance.Features.Content.Persistence.FitzKvImmutableContentStore(new InMemoryKvClient()));
        var events = new InMemoryEventStore();
        var controls = new StubControlReferenceDirectory(applicationId,
            ControlReference(tenantId, programId, applicationId, controlId));
        var evidenceSources = new StubEvidenceSourceReferenceDirectory(
            [EvidenceReference(tenantId, programId, controlId, artifactId)]);
        var handler = CreateHandler(tenantId, applicationId, sourceReader, applications,
            boundaries, controls, evidenceSources, new StubArtifactReadAccess(true), events);

        // Act
        var result = await handler.ReadForImportWorkerAsync(new PreviewApplicationChange(
            tenantId, applicationId, 1, "retire"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.EvidenceSourceReferences);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldRetainOriginalReferenceAndReportWithdrawalGivenWithdrawnAuthoritativeSupport()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var original = scenario.Support.View();
        scenario.Withdraw();

        // Act
        var result = await scenario.PreviewAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reference = Assert.Single(result.Value.EvidenceSourceReferences);
        Assert.Equal(scenario.Reference, reference with { SupportStatus = null });
        var status = Assert.IsType<EvidenceSupportStatusView>(reference.SupportStatus);
        Assert.Equal(original, status.Original);
        Assert.False(status.IsActive);
        Assert.Equal(2, status.Revision);
        Assert.Equal("Wrong target version", status.Withdrawal?.Reason);
        Assert.Equal(scenario.Support.Withdrawal!.ActorMemberId, status.Withdrawal?.ActorMemberId);
        Assert.Equal(scenario.Support.Withdrawal.OccurredAt, status.Withdrawal?.WithdrawnAt);
        Assert.DoesNotContain("evidence_sources", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldChangeImpactDigestGivenOnlySupportStatusChanges()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var before = await scenario.PreviewAsync();
        Assert.True(before.IsSuccess, before.Error?.Message);
        scenario.Withdraw();

        // Act
        var after = await scenario.PreviewAsync();

        // Assert
        Assert.True(after.IsSuccess, after.Error?.Message);
        Assert.NotEqual(before.Value.ImpactDigest, after.Value.ImpactDigest);
        Assert.Equal(Assert.Single(before.Value.EvidenceSourceReferences) with { SupportStatus = null },
            Assert.Single(after.Value.EvidenceSourceReferences) with { SupportStatus = null });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("program")]
    [InlineData("artifact")]
    [InlineData("target")]
    [InlineData("revision")]
    [InlineData("period")]
    [InlineData("purpose")]
    public async Task ShouldRefuseCompleteEvidenceContextGivenMissingOrMismatchedAuthoritativeSupport(string mismatch)
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        var authoritative = mismatch switch
        {
            "program" => scenario.Reference with { ProgramId = Uuid.CreateVersion4() },
            "artifact" => scenario.Reference with { ArtifactId = Uuid.CreateVersion4() },
            "target" => scenario.Reference with { ControlId = Uuid.CreateVersion4() },
            "revision" => scenario.Reference with { SupportedRecordRevision = 2 },
            "period" => scenario.Reference with { PeriodEnd = scenario.Reference.PeriodEnd.AddDays(1) },
            "purpose" => scenario.Reference with { Purpose = "Different purpose" },
            _ => scenario.Reference,
        };
        scenario.Reader.Support = mismatch == "missing" ? null : Linked(authoritative);

        // Act
        var result = await scenario.PreviewAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenWithdrawalAfterAuthoritativeStatusRead()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync();
        scenario.Reader.AfterSupportRead = async () =>
        {
            scenario.Withdraw();
            var original = scenario.Support.Current!;
            original.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                original.SubjectId, 1, original.OccurredAt));
            var withdrawal = scenario.Support.Withdrawal!;
            withdrawal.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                withdrawal.SubjectId, 2, withdrawal.OccurredAt));
            await scenario.Events.AppendAsync(new EventStreamAddress(scenario.Reference.TenantId.ToString(),
                "evidence-support", withdrawal.SubjectId.ToString()), 0, [original, withdrawal]);
        };

        // Act
        var result = await scenario.PreviewAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldOmitWithdrawalAttributionWithoutHydrationGivenRestrictedArtifact()
    {
        // Arrange
        var scenario = await Scenario.CreateAsync(canRead: false);
        scenario.Withdraw();

        // Act
        var result = await scenario.PreviewAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.EvidenceSourceReferences);
        Assert.Equal(0, scenario.Reader.SupportReads);
        Assert.Contains("evidence_sources", result.Value.PendingContexts);
    }

    static PreviewApplicationChangeHandler CreateHandler(Uuid tenantId, Uuid applicationId,
        IAggregateReader sourceReader, FitzApplicationDirectory applications,
        FitzApplicationBoundaryReferenceDirectory boundaries,
        IApplicationControlDraftReferenceDirectory controls,
        IApplicationEvidenceSourceReferenceDirectory evidenceSources,
        IApplicationEvidenceSourceAccess artifactReadAccess, InMemoryEventStore events)
    {
        var visibility = RestrictedApplicationVisibilityFixture.Create(sourceReader);
        var controlVersions = new FitzApplicationControlVersionReferenceDirectory(
            new InMemoryKvClient(), new ControlProjectionFacts().Content);
        return new PreviewApplicationChangeHandler(sourceReader, applications, boundaries,
            new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events), visibility,
            instanceConsistency: new SystemInstanceReadConsistency(applications, sourceReader,
                new LegacySystemInstanceSource(applications, events), events),
            controlVersions: controlVersions,
            controlVersionConsistency: new ApplicationControlVersionReferenceReadConsistency(
                controlVersions, events),
            evidenceSources: evidenceSources,
            evidenceSourceConsistency: new ApplicationEvidenceSourceReferenceReadConsistency(
                evidenceSources, events),
            evidenceSourceAccess: artifactReadAccess);
    }

    static DeclaredApplication CreateApplication(Uuid tenantId, Uuid applicationId)
    {
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, Uuid.CreateVersion4(),
            "Manager", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static ApplicationControlDraftReferenceView ControlReference(Uuid tenantId, Uuid programId,
        Uuid applicationId, Uuid controlId) => new(tenantId, "application", applicationId,
        programId, controlId, "AC-01", 1, Uuid.CreateVersion4(), "Payroll",
        "The control applies to payroll.");

    static ApplicationEvidenceSourceReferenceView EvidenceReference(Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid artifactId) => new(tenantId, programId,
        Uuid.CreateVersion4(), controlId, "control_version", 1, artifactId, 1,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Quarterly access evidence");

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory, Uuid tenantId,
        Uuid applicationId)
    {
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId, "Payroll",
            "Run payroll", null, Uuid.CreateVersion4(), "Manager", now, ApplicationFacts.Application(tenantId, applicationId), null));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class StubControlReferenceDirectory(Uuid applicationId,
        ApplicationControlDraftReferenceView reference) : IApplicationControlDraftReferenceDirectory
    {
        public ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<ApplicationControlDraftReferenceView>(subjectType == "application" &&
                recordId == applicationId ? [reference] : [], null));

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubEvidenceSourceReferenceDirectory(
        IReadOnlyList<ApplicationEvidenceSourceReferenceView> items,
        bool hasUnresolved = false, string? nextCursor = null)
        : IApplicationEvidenceSourceReferenceDirectory
    {
        public ValueTask<Page<ApplicationEvidenceSourceReferenceView>> ListByControlAsync(
            Uuid tenantId, Uuid controlId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(
            new Page<ApplicationEvidenceSourceReferenceView>(items.Where(item =>
                item.ControlId == controlId).Take(limit).ToArray(), nextCursor));

        public ValueTask<bool> HasUnresolvedReferencesAsync(Uuid tenantId,
            IReadOnlyCollection<Uuid> programIds, CancellationToken ct = default) =>
            ValueTask.FromResult(hasUnresolved && programIds.Count > 0);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class StubArtifactReadAccess(bool canRead) : IApplicationEvidenceSourceAccess
    {
        public ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid userId, Uuid artifactId,
            CancellationToken ct = default) => ValueTask.FromResult(canRead && userId != Uuid.Empty);
    }

    static EvidenceSupport Linked(ApplicationEvidenceSourceReferenceView reference)
    {
        var owner = new EvidenceSupport(reference.TenantId, reference.ProgramId, reference.EvidenceSupportId);
        var request = new LinkEvidenceSupport(reference.EvidenceSupportId, reference.TenantId,
            reference.ProgramId, reference.ArtifactId, reference.SupportedRecordType, reference.ControlId,
            reference.SupportedRecordRevision, reference.PeriodStart, reference.PeriodEnd, reference.Purpose,
            "Covers the selected access control.", 0);
        Assert.Null(owner.Link(request, reference.ArtifactRevision, new string('a', 64),
            ActorReference.ForMember(Uuid.CreateVersion4(), "Evidence owner"),
            new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)));
        return owner;
    }

    sealed class Scenario(ApplicationEvidenceSourceReferenceView reference, EvidenceSupport support,
        SourceReader reader, InMemoryEventStore events, PreviewApplicationChangeHandler handler,
        Uuid applicationId)
    {
        public ApplicationEvidenceSourceReferenceView Reference { get; } = reference;
        public EvidenceSupport Support { get; } = support;
        public SourceReader Reader { get; } = reader;
        public InMemoryEventStore Events { get; } = events;
        readonly Uuid _userId = Uuid.CreateVersion4();

        public ValueTask<Result<ApplicationChangePreview>> PreviewAsync() => handler.ReadAsync(
            new PreviewApplicationChange(Reference.TenantId, applicationId, 1, "retire"),
            _userId, CancellationToken.None);

        public void Withdraw() => Assert.Null(Support.Withdraw(new WithdrawEvidenceSupport(
            Uuid.CreateVersion4(), Reference.TenantId, Reference.ProgramId, Reference.EvidenceSupportId,
            1, "Wrong target version"), ActorReference.ForMember(Uuid.CreateVersion4(), "Evidence lead"),
            Support.Current!.OccurredAt.AddMinutes(1)));

        public static async Task<Scenario> CreateAsync(bool canRead = true)
        {
            var tenantId = Uuid.CreateVersion4();
            var applicationId = Uuid.CreateVersion4();
            var controlId = Uuid.CreateVersion4();
            var programId = Uuid.CreateVersion4();
            var reference = EvidenceReference(tenantId, programId, controlId, Uuid.CreateVersion4());
            var support = Linked(reference);
            var reader = new SourceReader(CreateApplication(tenantId, applicationId)) { Support = support };
            var kv = new InMemoryKvClient();
            var applications = new FitzApplicationDirectory(kv);
            await ProjectApplicationAsync(applications, tenantId, applicationId);
            var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv,
                new FitzKvImmutableContentStore(kv));
            var controls = new StubControlReferenceDirectory(applicationId,
                ControlReference(tenantId, programId, applicationId, controlId));
            var evidence = new StubEvidenceSourceReferenceDirectory([reference]);
            var events = new InMemoryEventStore();
            var handler = CreateHandler(tenantId, applicationId, reader, applications, boundaries,
                controls, evidence, new StubArtifactReadAccess(canRead), events);
            return new Scenario(reference, support, reader, events, handler, applicationId);
        }
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public EvidenceSupport? Support { get; set; }
        public Func<ValueTask>? AfterSupportRead { get; set; }
        public int SupportReads { get; private set; }

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is not EvidenceSupport)
                return (TAggregate)source;
            SupportReads++;
            var current = Support is { } support && support.Id == aggregate.Id
                ? (TAggregate)(Aggregate)support : aggregate;
            if (AfterSupportRead is { } afterRead)
            {
                AfterSupportRead = null;
                await afterRead();
            }
            return current;
        }
    }
}
