using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Artifacts.Models;
using Bdgrz.Compliance.Features.Artifacts.Persistence;
using Bdgrz.Compliance.Features.Artifacts.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class RiskTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
    static readonly Uuid ActorMemberId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerMemberId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly DateOnly TypeIDate = new(2027, 3, 31);
    static readonly DateTimeOffset CreatedAt = new(2027, 2, 1, 12, 0, 0,
        TimeSpan.Zero);
    static readonly DateTimeOffset RevisedAfterTypeI = new(2027, 4, 1, 12, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureRiskAtTypeIDateGivenLaterDraftRevision()
    {
        // Arrange
        await using var fixture = await Fixture();

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("risks", result.Value.Family);
        var source = Assert.Single(result.Value.Sources);
        Assert.Equal("risk", source.Kind);
        Assert.Equal(RiskId, source.RecordId);
        Assert.Equal(1, source.Revision);
        Assert.Equal(Uuid.CreateVersion5(RiskId, "risk_v1:2027-03-31"), source.VersionId);
        Assert.Equal(64, source.ContentSha256.Length);
    }

    [Fact]
    public async Task ShouldCaptureAssessmentMethodAndOwnerGivenCompleteRiskAtTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
        Assert.Empty(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldExcludeFutureDatedAssessmentGivenItWasRecordedBeforeTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            assessmentAt: RevisedAfterTypeI, assessmentRecordedAt: CreatedAt);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker =>
            blocker.Contains("no inherent assessment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldExcludeBackdatedAssessmentGivenItWasRecordedAfterTypeIDate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            assessmentAt: CreatedAt, assessmentRecordedAt: RevisedAfterTypeI);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker =>
            blocker.Contains("no inherent assessment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenEvaluationDirectoryBehindEvents()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            evaluationDirectoryCaughtUp: false);

        // Act
        var result = await fixture.Contributor.ReadAsync(Engagement(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRequireAcceptedControlTreatmentToMatchCapturedControlVersionGivenMitigate()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            includeAcceptedControlTreatment: true);
        var controlVersion = new TypeIBaselineSourceReference("control_version", ControlId,
            ControlVersionId, 1, new string('a', 64));
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [controlVersion]);

        // Act
        var missingControl = await fixture.Contributor.ReadAsync(Engagement(),
            new TypeIBaselineSourceReadContext(Uuid.Empty, []), CancellationToken.None);
        var capturedControl = await fixture.Contributor.ReadAsync(Engagement(), context,
            CancellationToken.None);

        // Assert
        Assert.True(missingControl.IsSuccess, missingControl.Error?.Message);
        Assert.Contains(missingControl.Value.Blockers, blocker =>
            blocker.Contains("accepted control treatment", StringComparison.Ordinal));
        Assert.True(capturedControl.IsSuccess, capturedControl.Error?.Message);
        Assert.DoesNotContain(capturedControl.Value.Blockers, blocker =>
            blocker.Contains("accepted control treatment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldArchiveExactSelectedRiskNarrativesGivenProductionNativeSourcePublication()
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true);
        var root = Directory.CreateTempSubdirectory("bdgrz-risk-source-archive-").FullName;
        try
        {
            var content = fixture.Provider.GetRequiredService<IImmutableContentStore>();
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(root, null,
                ArtifactContentLimits.MaxContentLength), TimeProvider.System);
            var publisher = new PublishedTypeIBaselinePreviewReader(
                new TypeIBaselinePreviewReader([fixture.Contributor]), artifacts, content);
            var preview = await publisher.ReadAsync(Engagement());
            Assert.True(preview.IsSuccess, preview.Error?.Message);
            var source = Assert.Single(preview.Value.Sources);
            Assert.NotNull(source.Content);
            var selected = new AuditPackageSelectionEntry("risks", "R-01", source.Kind,
                source.RecordId, source.Revision, source.Content.FileExtension,
                source.ContentSha256, source.Content.Bytes, "product_calculated", source.VersionId,
                source.Content);
            var manifest = new AuditPackageManifestView(TenantId, ProgramId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), preview.Value.ContentSha256,
                [RiskId], [new AuditPackageManifestEntryView(selected, "risks/R-01." + selected.Extension)],
                new string('a', 64), CreatedAt);

            // Act
            var built = await new AuditPackageArchiveBuilder(artifacts).BuildAsync(TenantId, manifest,
                CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            await using var stored = await artifacts.OpenVerifiedAsync(new ArtifactContentReference(TenantId,
                built.Value!.ArchiveSha256!, built.Value.ArchiveBytes!.Value));
            using var zip = new ZipArchive(stored!, ZipArchiveMode.Read);
            var entry = Assert.Single(zip.Entries);
            await using var payload = entry.Open();
            using var envelope = await JsonDocument.ParseAsync(payload);
            var proof = Convert.FromBase64String(envelope.RootElement.GetProperty("source_base64").GetString()!);
            var blobs = envelope.RootElement.GetProperty("content_blobs").EnumerateArray().ToArray();
            var recovered = new List<string>();
            foreach (var blob in blobs)
            {
                var reference = blob.GetProperty("reference").Deserialize(ComplianceCoreJsonContext.Default.ImmutableContentRef)!;
                var bytes = Convert.FromBase64String(blob.GetProperty("base64").GetString()!);
                Assert.Equal(reference.Bytes, bytes.LongLength);
                Assert.Equal(reference.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                Assert.Equal((await content.ReadVerifiedAsync(TenantId.ToString(), reference)).ToArray(), bytes);
                recovered.Add(Encoding.UTF8.GetString(bytes));
            }

            // Assert
            Assert.Equal(source.ContentSha256, Convert.ToHexStringLower(SHA256.HashData(proof)));
            Assert.Equal(source.ContentSha256, envelope.RootElement.GetProperty("logical_content_sha256").GetString());
            Assert.Equal("application/vnd.bdgrz.risk-snapshot-source.v1+json", source.Content.MediaType);
            Assert.Equal(source.Content.Bytes, entry.Length);
            Assert.Contains(recovered, body => body.Contains("Credential misuse", StringComparison.Ordinal));
            Assert.Contains(recovered, body => body.Contains("A compromised credential could expose customer records.", StringComparison.Ordinal));
            Assert.Contains(recovered, body => body.Contains("Risk owner is accountable for the mitigation.", StringComparison.Ordinal));
            Assert.DoesNotContain(recovered, body => body.Contains("Revised credential misuse", StringComparison.Ordinal));
            Assert.DoesNotContain(recovered, body => body.Contains("Updated after the audit date.", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnClassifiedPublicationFailureGivenUnverifiableRiskBusinessBytes(bool unavailable)
    {
        // Arrange
        await using var fixture = await Fixture(includeAssessmentAndOwner: true,
            sourceContent: content => new UnavailableBusinessContent(content, unavailable));
        var root = Directory.CreateTempSubdirectory("bdgrz-risk-source-failure-").FullName;
        try
        {
            var artifacts = new LocalArtifactContentStore(new ArtifactContentStoreOptions(root, null,
                ArtifactContentLimits.MaxContentLength), TimeProvider.System);
            var publisher = new PublishedTypeIBaselinePreviewReader(new TypeIBaselinePreviewReader([fixture.Contributor]),
                artifacts, fixture.Provider.GetRequiredService<IImmutableContentStore>());

            // Act
            var result = await publisher.ReadAsync(Engagement());

            // Assert
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.Equal(unavailable, result.Error!.IsTransient);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", TypeIDate, null, ["security"],
        Uuid.CreateVersion4(), [], "draft", 1,
        new DateTimeOffset(TypeIDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
        ActorMemberId, "Examiner");

    static async Task<FixtureState> Fixture(bool includeAssessmentAndOwner = false,
        DateTimeOffset? assessmentAt = null, DateTimeOffset? assessmentRecordedAt = null,
        bool evaluationDirectoryCaughtUp = true, bool includeAcceptedControlTreatment = false,
        Func<IImmutableContentStore, IImmutableContentStore>? sourceContent = null)
    {
        var risk = new RiskDraftRules(TenantId, RiskId);
        Assert.True(risk.Create(ProgramId, RiskId, "R-01",
            new RiskDraftContent("Credential misuse", "A credential may be misused.",
                "Unauthorized access to customer data.", "Security review."), ActorMemberId,
            "Risk owner", CreatedAt).IsSuccess);
        Assert.Null(risk.Revise(ProgramId, 1,
            new RiskDraftContent("Revised credential misuse", "A credential may be misused.",
                "Unauthorized access to customer data.", "Updated after the audit date."),
            ActorMemberId, "Risk owner", RevisedAfterTypeI));

        var method = new RiskMethodRules(TenantId, ProgramId);
        var evaluation = new RiskEvaluationRules(TenantId, RiskId);
        if (includeAssessmentAndOwner)
        {
            var actor = ActorReference.ForMember(ActorMemberId, "Risk owner");
            Assert.Null(method.Publish(ProgramId, 0, ["Rare", "Unlikely", "Possible",
                "Likely", "Almost certain"], ["Negligible", "Minor", "Moderate", "Major",
                "Severe"], 12, actor, CreatedAt));
            Assert.Null(evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
                method.Current!, RiskEvaluation.Inherent, 3, 4,
                "A compromised credential could expose customer records.", ActorMemberId,
                "Risk owner", assessmentAt ?? CreatedAt));
            if (assessmentRecordedAt is null)
                Assert.Null(evaluation.ChooseTreatment(ProgramId, 1, "mitigate",
                    "Limit credential exposure.", ActorMemberId, "Risk owner",
                    CreatedAt.AddHours(1)));
        }

        var store = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        services.AddPortia();
        var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<IAggregateReader>();
        var content = provider.GetRequiredService<IImmutableContentStore>();
        if (includeAssessmentAndOwner)
        {
            var actor = ActorReference.ForMember(ActorMemberId, "Risk owner");
            await RiskSeeds.AssignOwnerAsync(provider, TenantId, ProgramId, RiskId, Uuid.CreateVersion4(),
                ActorMemberId, "Risk owner is accountable for the mitigation.", actor, CreatedAt);
            if (includeAcceptedControlTreatment)
            {
                var treatmentId = Uuid.CreateVersion4();
                await RiskSeeds.ProposeTreatmentAsync(provider, TenantId, ProgramId, RiskId, treatmentId,
                    ControlId, ControlVersionId, "The approved control addresses this credential risk.",
                    ActorMemberId, actor, CreatedAt);
                await RiskSeeds.ReviewTreatmentAsync(provider, TenantId, ProgramId, RiskId, ControlId,
                    treatmentId, "accept", "The control treatment is supported.",
                    ActorReference.ForMember(ReviewerMemberId, "Reviewer"), CreatedAt.AddHours(2));
            }
        }
        var stream = new EventStreamAddress(TenantId.ToString(), "risks", RiskId.ToString());
        var riskOwner = await RiskDraftSeeds.OwnerAsync(content, risk);
        await store.AppendAsync(stream, 0, new AggregateScenario<RiskDraft>(riskOwner).PendingEvents.ToArray());
        var draftDirectory = new RiskDraftDirectory(Summary(risk),
            await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risks")));
        if (method.PublishedVersion > 0)
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "risk-methods",
                method.Id.ToString()), 0,
                new AggregateScenario<RiskMethod>(await RiskMethodSeeds.OwnerAsync(content, method)).PendingEvents.ToArray());
        if (evaluation.Revision > 0)
        {
            var evaluationOwner = await RiskEvaluationSeeds.OwnerAsync(content, evaluation);
            var evaluationEvents = new AggregateScenario<RiskEvaluation>(evaluationOwner).PendingEvents.ToArray();
            if (assessmentRecordedAt is { } recordedAt &&
                evaluationEvents[0] is RiskAssessmentRecorded recorded)
            {
                DomainEvent stamped = recorded with { };
                stamped.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), RiskId, 1,
                    recordedAt));
                evaluationEvents[0] = stamped;
            }
            await store.AppendAsync(new EventStreamAddress(TenantId.ToString(), "risk-evaluations",
                RiskId.ToString()), 0, evaluationEvents);
        }
        var evaluationCheckpoint = evaluationDirectoryCaughtUp
            ? await CheckpointAfterAsync(store, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risk-evaluations"))
            : new ProjectionCheckpoint(EventCursor.Start);
        var evaluationDirectory = new RiskEvaluationDirectory(evaluationCheckpoint,
            evaluation.Revision > 0 ? evaluation.ToView() : null);
        return new FixtureState(new RiskTypeIBaselineSourceContributor(draftDirectory,
            evaluationDirectory, new RiskGovernanceReader(reader, store, content), sourceContent?.Invoke(content) ?? content,
            reader, store), provider);
    }

    static RiskDraftView Summary(RiskDraftRules risk) => new(TenantId, ProgramId, RiskId,
        "R-01", risk.Revision, "draft_unassessed", "unresolved",
        new RiskDraftContent("Revised credential misuse", "A credential may be misused.",
            "Unauthorized access to customer data.", "Updated after the audit date."),
        ActorMemberId, "Risk owner", RevisedAfterTypeI);

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore store,
        EventStreamPattern pattern)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in store.ReadAsync(pattern, cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class UnavailableBusinessContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.Schema == RiskEvaluationContent.AuthoritySchema
            ? throw (unavailable ? new ImmutableContentUnavailableException("The risk body is temporarily unavailable.")
                : new ImmutableContentIntegrityException("The risk body failed verification."))
            : inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed record FixtureState(ITypeIBaselineSourceContributor Contributor, ServiceProvider Provider) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class RiskDraftDirectory(RiskDraftView summary, ProjectionCheckpoint checkpoint)
        : IRiskDraftDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult<RiskDraftView?>(summary);

        public ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskDraftView>([summary], null));

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<RiskDraftRevisionView?>(null);
    }

    sealed class RiskEvaluationDirectory(ProjectionCheckpoint checkpoint,
        RiskEvaluationView? current) : IRiskEvaluationDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(checkpoint);

        public ValueTask<RiskEvaluationView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult(current);

        public ValueTask<Page<RiskEvaluationHistoryEntryView>> ListHistoryAsync(Uuid tenantId,
            Uuid riskId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskEvaluationHistoryEntryView>([], null));
    }

}
