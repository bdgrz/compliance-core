using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration;

public sealed class AdvisorRecordVersionResolverTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldAcceptCurrentControlDraftGivenExactProgramSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedControlAsync();
        var selection = AdvisorRecordVersion.ControlDraft(control.Id, control.DraftVersionId,
            control.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldReturnExactControlDraftContentGivenCurrentSelectedSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedControlAsync();
        var selected = AdvisorRecordVersion.ControlDraft(control.Id, control.DraftVersionId,
            control.Revision);
        var reader = new AdvisorSharedSourceReader(fixture.Controls, fixture.PolicySources, fixture.Resolver(),
            fixture.Metadata, new BoundaryReader(null), fixture.Readiness);
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await reader.GetAsync(fixture.TenantId, fixture.ProgramId, handoffId,
            selected);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(control.CurrentContent),
            System.Text.Json.JsonSerializer.Serialize(result.Value.ControlDraftContent));
        Assert.Equal(selected, result.Value.SelectedSource);
        Assert.Null(result.Value.ControlVersion);
        Assert.Null(result.Value.EvidenceArtifact);
    }

    [Fact]
    public async Task ShouldRejectStaleControlDraftGivenNewerCurrentRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedControlAsync();
        var selection = AdvisorRecordVersion.ControlDraft(control.Id, control.DraftVersionId,
            control.Revision + 1);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAcceptApprovedControlGivenExactImmutableVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedApprovedControlAsync();
        var version = Assert.Single(control.ReadVersions());
        var selection = AdvisorRecordVersion.ControlVersion(control.Id, version.VersionId,
            version.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectSupersededApprovedControlGivenOlderExactVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedControlWithSuccessorAsync();
        var superseded = control.ReadVersions()[0];
        var selection = AdvisorRecordVersion.ControlVersion(control.Id, superseded.VersionId,
            superseded.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAcceptPolicyDraftGivenCurrentExactRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var policy = await fixture.SeedPolicyDraftAsync();
        var selection = AdvisorRecordVersion.PolicyDraft(policy.Id, policy.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldAcceptApprovedPolicyGivenExactNumericVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var policy = await fixture.SeedApprovedPolicyAsync();
        var version = Assert.Single(policy.ReadVersions());
        var selection = AdvisorRecordVersion.PolicyVersion(policy.Id, version.Version,
            version.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectSupersededApprovedPolicyGivenOlderNumericVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var policy = await fixture.SeedPolicyWithSuccessorAsync();
        var superseded = policy.ReadVersions()[0];
        var selection = AdvisorRecordVersion.PolicyVersion(policy.Id, superseded.Version,
            superseded.Revision);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAcceptBoundaryQuestionGivenExactDraftEntry()
    {
        // Arrange
        await using var fixture = new Fixture();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var entryId = Uuid.CreateVersion4();
        var version = new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
            versionId, 3, new BoundaryContent("Scope", "readiness", [],
                [new(entryId, "question", "control", "AC-01", null, "Owner",
                    "Confirm the population", true)]), "draft", null,
            fixture.MemberId, "Lead", Now);
        var reader = new BoundaryReader(new BoundaryView(fixture.TenantId, boundaryId,
            fixture.ProgramId, version, null, null, 5));
        var selection = AdvisorRecordVersion.BoundaryQuestion(boundaryId, entryId, versionId, 3);

        // Act
        var result = await fixture.Resolver(boundaries: reader).RequireCurrentAsync(
            fixture.TenantId, fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectBoundaryQuestionGivenEntryAbsentFromExactVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var entryId = Uuid.CreateVersion4();
        var version = new BoundaryVersionView(fixture.TenantId, boundaryId, fixture.ProgramId,
            versionId, 3, new BoundaryContent("Scope", "readiness", [],
                [new(Uuid.CreateVersion4(), "question", "control", "AC-01", null, "Owner",
                    "Confirm the population", true)]), "draft", null,
            fixture.MemberId, "Lead", Now);
        var reader = new BoundaryReader(new BoundaryView(fixture.TenantId, boundaryId,
            fixture.ProgramId, version, null, null, 5));
        var selection = AdvisorRecordVersion.BoundaryQuestion(boundaryId, entryId, versionId, 3);

        // Act
        var result = await fixture.Resolver(boundaries: reader).RequireCurrentAsync(
            fixture.TenantId, fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectBoundaryQuestionGivenSupersededVersion()
    {
        // Arrange
        await using var fixture = new Fixture();
        var boundaryId = Uuid.CreateVersion4();
        var selectedVersionId = Uuid.CreateVersion4();
        var currentVersionId = Uuid.CreateVersion4();
        var entryId = Uuid.CreateVersion4();
        var selectedVersion = new BoundaryVersionView(fixture.TenantId, boundaryId,
            fixture.ProgramId, selectedVersionId, 3, new BoundaryContent("Old scope", "readiness",
                [], [new(entryId, "question", "control", "AC-01", null, "Owner",
                    "Confirm the population", true)]), "superseded", null,
            fixture.MemberId, "Lead", Now);
        var currentVersion = selectedVersion with
        {
            VersionId = currentVersionId,
            Revision = 5,
            Content = new BoundaryContent("Current scope", "readiness", [], []),
            Status = "approved",
        };
        var reader = new BoundaryReader(new BoundaryView(fixture.TenantId, boundaryId,
            fixture.ProgramId, null, currentVersion, null, 5), selectedVersion);
        var selection = AdvisorRecordVersion.BoundaryQuestion(boundaryId, entryId,
            selectedVersionId, 3);

        // Act
        var result = await fixture.Resolver(boundaries: reader).RequireCurrentAsync(
            fixture.TenantId, fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAcceptReadinessGapGivenLatestAssessmentFingerprint()
    {
        // Arrange
        await using var fixture = new Fixture();
        var gapId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        const string fingerprint = "a4f7c33f90bb60b48d55e5fc80bde66920d41fb327fbd91424f24ba7a42a26d0";
        var assessment = new ReadinessAssessmentView(fixture.TenantId, fixture.ProgramId,
            assessmentId, 4, "rules-v1", Now, null, fingerprint, [], [],
            [new(gapId, "control", "AC-01", "rule-1", "The control is missing", [])],
            0, 1, ActorReference.ForMember(fixture.MemberId, "Lead"), Now, null);
        var reader = await fixture.SeedReadinessAsync(assessmentId, assessment);
        var selection = AdvisorRecordVersion.ReadinessGap(gapId, assessmentId, fingerprint);

        // Act
        var result = await fixture.Resolver(readiness: reader).RequireCurrentAsync(
            fixture.TenantId, fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectReadinessGapGivenSupersededAssessment()
    {
        // Arrange
        await using var fixture = new Fixture();
        var gapId = Uuid.CreateVersion4();
        var assessmentId = Uuid.CreateVersion4();
        var latestAssessmentId = Uuid.CreateVersion4();
        const string fingerprint = "a4f7c33f90bb60b48d55e5fc80bde66920d41fb327fbd91424f24ba7a42a26d0";
        var assessment = new ReadinessAssessmentView(fixture.TenantId, fixture.ProgramId,
            assessmentId, 4, "rules-v1", Now, null, fingerprint, [], [],
            [new(gapId, "control", "AC-01", "rule-1", "The control is missing", [])],
            0, 1, ActorReference.ForMember(fixture.MemberId, "Lead"), Now, null);
        var reader = await fixture.SeedReadinessAsync(latestAssessmentId, assessment);
        var selection = AdvisorRecordVersion.ReadinessGap(gapId, assessmentId, fingerprint);

        // Act
        var result = await fixture.Resolver(readiness: reader).RequireCurrentAsync(
            fixture.TenantId, fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAcceptEvidenceArtifactGivenCurrentProgramSupportLink()
    {
        // Arrange
        await using var fixture = new Fixture();
        var control = await fixture.SeedControlAsync();
        var artifact = await fixture.SeedArtifactAsync();
        var request = new LinkEvidenceSupport(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, artifact.Id,
            "control", control.Id, control.Revision, new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31), "Design review", "Supports the control design.", 0);
        var linkedArtifactRevision = artifact.Revision;
        var support = new EvidenceSupport(fixture.TenantId, fixture.ProgramId,
            EvidenceSupport.IdFor(request));
        Assert.Null(support.Link(request, linkedArtifactRevision, artifact.ContentSha256!,
            ActorReference.ForMember(fixture.MemberId, "Lead"), Now));
        await fixture.Writer.SaveAsync(support, fixture.SystemDispatch);
        Assert.Null(artifact.RecordDownload(linkedArtifactRevision, Uuid.CreateVersion4(),
            "Advisor review", ActorReference.ForMember(fixture.MemberId, "Lead"),
            Now.AddMinutes(1), artifact.ContentSha256!, artifact.ContentLength!.Value));
        await fixture.Writer.SaveAsync(artifact, fixture.SystemDispatch);
        var metadata = await fixture.Metadata.GetAsync(fixture.TenantId, artifact.Id,
            CancellationToken.None);
        Assert.True(metadata.IsSuccess, metadata.Error?.Message);
        var selection = AdvisorRecordVersion.EvidenceArtifact(artifact.Id, linkedArtifactRevision,
            (ulong)linkedArtifactRevision, metadata.Value.ContentSha256, support.Id);

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRejectLegacyUuidPairGivenUnresolvedSourceFamily()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selection = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldHideEvidenceSupportOutsideRequestedProgramGivenTenantArtifact()
    {
        // Arrange
        await using var fixture = new Fixture();
        var selection = AdvisorRecordVersion.EvidenceArtifact(Uuid.CreateVersion4(), 2, 2,
            new string('a', 64), Uuid.CreateVersion4());

        // Act
        var result = await fixture.Resolver().RequireCurrentAsync(fixture.TenantId,
            fixture.ProgramId, [selection]);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid MemberId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IDomainEventReader Events { get; }
        public EvidenceArtifactMetadataRead Metadata { get; }
        public ReadinessReader Readiness { get; }
        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        public ControlReader Controls => new(Reader, Events, Content);
        public PolicyReader PolicySources => new(Reader, Events, Content);
        public RequestDispatchContext SystemDispatch { get; } =
            new(RequestActor.System);

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            Metadata = new EvidenceArtifactMetadataRead(Reader, Events);
            Readiness = new ReadinessReader(Reader, _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        }

        public AdvisorRecordVersionResolver Resolver(IBoundaryDirectoryReader? boundaries = null,
            ReadinessReader? readiness = null) => new(Reader, Controls, PolicySources, Metadata,
            new EvidenceSupportTargetSource(Reader, Controls, EmptyOperations.Reader(Reader, Events), EmptyOperations.Evaluations(Reader, Events)), boundaries ?? new BoundaryReader(null),
            readiness ?? Readiness);

        public async Task<ReadinessReader> SeedReadinessAsync(Uuid latest, ReadinessAssessmentView assessment)
        {
            var content = _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            var executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            var evaluation = new ReadinessEvaluation(assessment.Inputs, assessment.Findings, assessment.Gaps,
                assessment.InputFingerprint)
            { AccessReviewScopes = assessment.AccessReviewScopes };
            await ReadinessSeed.AdoptAsync(Reader, executor, content, TenantId, ProgramId,
                assessment.AssessmentId, evaluation, assessment.AsOf, MemberId, Now);
            if (latest != assessment.AssessmentId)
                await ReadinessSeed.AdoptAsync(Reader, executor, content, TenantId, ProgramId,
                    latest, evaluation, assessment.AsOf, MemberId, Now.AddMinutes(1));
            return Readiness;
        }

        public async Task<ControlRules> SeedControlAsync()
        {
            var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
            var control = new ControlRules(TenantId, controlId);
            Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-01",
                new ControlDraftContent("Access review", "Review access", "Management reviews access",
                    "The owner reviews the access list quarterly.", ["Dated approval record"]),
                MemberId, "Lead", Now).IsSuccess);
            await ControlSeeds.AppendAsync(Reader, Writer, Content, control, CancellationToken.None);
            return control;
        }

        public async Task<PolicyRules> SeedApprovedPolicyAsync()
        {
            var policy = await SeedPolicyDraftAsync();
            var author = ActorReference.ForMember(MemberId, "Author");
            var reviewerId = Uuid.CreateVersion4();
            var approverId = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            Assert.Null(policy.Review(ProgramId, policy.Revision, reviewId, "accept", "Reviewed",
                ActorReference.ForMember(reviewerId, "Reviewer"), reviewerId, Now.AddMinutes(1)));
            Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), reviewId,
                new DateOnly(2026, 1, 1), true, "Approved", null,
                ActorReference.ForMember(approverId, "Approver"), approverId,
                Now.AddMinutes(2)));
            await PolicySeeds.AppendAsync(Reader, Writer, Content, policy, CancellationToken.None);
            return policy;
        }

        public async Task<PolicyRules> SeedPolicyWithSuccessorAsync()
        {
            var policy = await SeedApprovedPolicyAsync();
            Assert.Null(policy.ProposeSuccessor(ProgramId, 1,
                new PolicyContent("Access policy v2", "Protect access", "core_security", [], 12,
                    "Access must be reviewed quarterly.", null, "Security owner", []),
                ActorReference.ForMember(MemberId, "Author"), MemberId, Now.AddMinutes(3)));
            var reviewerId = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            Assert.Null(policy.Review(ProgramId, policy.Revision, reviewId, "accept", "Reviewed",
                ActorReference.ForMember(reviewerId, "Reviewer"), reviewerId, Now.AddMinutes(4)));
            var approverId = Uuid.CreateVersion4();
            Assert.Null(policy.Approve(ProgramId, policy.Revision, Uuid.CreateVersion4(), reviewId,
                new DateOnly(2027, 1, 1), false, "Approved", "successor-impact",
                ActorReference.ForMember(approverId, "Approver"), approverId,
                Now.AddMinutes(5)));
            await PolicySeeds.AppendAsync(Reader, Writer, Content, policy, CancellationToken.None);
            return policy;
        }

        public async Task<PolicyRules> SeedPolicyDraftAsync()
        {
            var policy = new PolicyRules(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-01"));
            var content = new PolicyContent("Access policy", "Protect access", "core_security",
                [], 12, "Access must be reviewed quarterly.", null, "Security owner", []);
            Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-01", content,
                ActorReference.ForMember(MemberId, "Author"), MemberId, Now).IsSuccess);
            await PolicySeeds.AppendAsync(Reader, Writer, Content, policy, CancellationToken.None);
            return policy;
        }

        public async Task<ControlRules> SeedApprovedControlAsync()
        {
            var control = new ControlRules(TenantId, ControlDraft.IdFor(TenantId, ProgramId, "AC-02"));
            var authorId = MemberId;
            var ownerId = Uuid.CreateVersion4();
            var reviewerId = Uuid.CreateVersion4();
            var approverId = Uuid.CreateVersion4();
            Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-02",
                new ControlDraftContent("Access review", "Review access",
                    "Management reviews access", "The security lead reviews access quarterly.",
                    ["Dated review record"], "Security lead"), authorId, "Author", Now).IsSuccess);
            var scope = new ResponsibilityScope("control", control.Id, control.DraftVersionId,
                control.Revision);
            Assert.Null(control.AssignResponsibility(scope, Uuid.CreateVersion4(), ownerId,
                ResponsibilityType.ControlOwner, authorId, "Author", Now.AddMinutes(1), Now,
                null, []));
            var reviewId = Uuid.CreateVersion4();
            Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept",
                "Reviewed", reviewerId, "Reviewer", Now.AddMinutes(2)));
            Assert.Null(control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(),
                reviewId, new DateOnly(2026, 1, 1), "Approved", new HashSet<Uuid> { ownerId },
                approverId, "Approver", Now.AddMinutes(3)));
            await ControlSeeds.AppendAsync(Reader, Writer, Content, control, CancellationToken.None);
            return control;
        }

        public async Task<ControlRules> SeedControlWithSuccessorAsync()
        {
            var control = await SeedApprovedControlAsync();
            var prior = control.ApprovedVersion!;
            Assert.Null(control.ProposeSuccessor(ProgramId, prior.VersionId,
                prior.Content with { Title = "Access review v2" }, MemberId, "Author",
                Now.AddMinutes(4)));
            var ownerId = Uuid.CreateVersion4();
            var scope = new ResponsibilityScope("control", control.Id, control.DraftVersionId,
                control.Revision);
            Assert.Null(control.AssignResponsibility(scope, Uuid.CreateVersion4(), ownerId,
                ResponsibilityType.ControlOwner, MemberId, "Author", Now.AddMinutes(5),
                Now.AddMinutes(3), null, []));
            var reviewId = Uuid.CreateVersion4();
            Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept", "Reviewed",
                Uuid.CreateVersion4(), "Reviewer", Now.AddMinutes(6)));
            Assert.Null(control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(), reviewId,
                new DateOnly(2027, 1, 1), "Successor ready.", new HashSet<Uuid> { ownerId },
                Uuid.CreateVersion4(), "Approver", Now.AddMinutes(7), impactDigest: "digest"));
            await ControlSeeds.AppendAsync(Reader, Writer, Content, control, CancellationToken.None);
            return control;
        }

        public async Task<EvidenceArtifact> SeedArtifactAsync()
        {
            var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
            Assert.True(artifact.Register(new EvidenceArtifactContent("Audit evidence", null,
                    "document", "Manual upload", Now.AddMinutes(-1), new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31), "restricted"), new string('a', 64), 128,
                ActorReference.ForMember(MemberId, "Collector"), Now.AddMinutes(-1)).IsSuccess);
            Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now));
            await Writer.SaveAsync(artifact, SystemDispatch);
            return artifact;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class BoundaryReader(BoundaryView? current, BoundaryVersionView? historical = null)
        : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult(current);

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult(
            historical?.BoundaryId == boundaryId && historical.VersionId == versionId
                ? historical
                : null);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

}
