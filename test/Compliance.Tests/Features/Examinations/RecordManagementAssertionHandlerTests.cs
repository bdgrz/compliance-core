using Bdgrz.Compliance.Tests.Features.SystemDescription;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackageRules;
using ManagementAssertionAggregate = Bdgrz.Compliance.Features.Examinations.ManagementAssertion;
using SystemDescriptionAggregate = Bdgrz.Compliance.Features.SystemDescription.SystemDescriptionRules;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class RecordManagementAssertionHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRetryUnavailableDescriptionContentAndRefuseCorruptionGivenAssertionSources(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var descriptions = new SystemDescriptionReader(fixture.Reader, fixture.PeriodEvents, new FailingPackageContentStore(unavailable));
        var handler = new RecordManagementAssertionHandler(fixture.Executor, fixture.Reader, fixture.Packages,
            fixture.Periods, fixture.Clock, descriptions);

        // Act
        var result = await handler.HandleAsync(fixture.Context(fixture.Request(sources.Content)), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertionAggregate(fixture.TenantId, fixture.ProgramId, fixture.AssertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Null(saved.Current);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRetryUnavailablePackageContentAndRefuseCorruptionGivenAssertionSources(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var packages = new AuditPackageReader(fixture.Reader, new FailingPackageContentStore(unavailable));
        var handler = new RecordManagementAssertionHandler(fixture.Executor, fixture.Reader, packages, fixture.Periods, fixture.Clock, fixture.Descriptions);

        // Act
        var result = await handler.HandleAsync(fixture.Context(fixture.Request(sources.Content)), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertionAggregate(fixture.TenantId,
            fixture.ProgramId, fixture.AssertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRecordExactAssertionGivenValidatedPackageAndApprovedBoundDescription()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertionAggregate(
            fixture.TenantId, fixture.ProgramId, request.AssertionId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.Current!.Status);
        Assert.Equal(sources.Content.AssertionText,
            saved.Current.CurrentVersion!.Content.AssertionText);
        Assert.Equal(sources.Content.PackageManifestId,
            saved.Current.CurrentVersion.Content.PackageManifestId);
        Assert.Equal(fixture.DescriptionVersionId,
            saved.Current.CurrentVersion.Content.SystemDescriptionVersionId);
    }

    [Fact]
    public async Task ShouldRejectUnvalidatedPackageGivenAssertionRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync(validatePackage: false);
        var request = fixture.Request(sources.Content);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertionAggregate(
            fixture.TenantId, fixture.ProgramId, request.AssertionId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(saved.Current);
        Assert.Equal(0, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectChangedDescriptionDigestGivenExactVersionIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content with
        { SystemDescriptionSha256 = Hash('f') });

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectChangedSupportingInputGivenValidatedPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content with
        { SupportingInputs = [sources.Criteria with { ContentSha256 = Hash('f') }, sources.Control] });

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectDifferentRouteExaminationGivenAssertionContent()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content) with
        { ExaminationId = Uuid.CreateVersion4() };

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectMisattributedWordingArtifactGivenAuditorWording()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync(wordingArtifactSource: "management");
        var request = fixture.Request(sources.Content);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldTreatSameAssertionRetryAsIdempotentGivenLaterHandlerTime()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content);
        var context = fixture.Context(request);
        Assert.True((await fixture.Handler().HandleAsync(context, CancellationToken.None)).IsSuccess);

        // Act
        fixture.Clock.Set(Now.AddMinutes(9));
        var retry = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new ManagementAssertionAggregate(
            fixture.TenantId, fixture.ProgramId, request.AssertionId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(Now.AddMinutes(8), saved.Current!.CurrentVersion!.AuthoredAt);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenAssertionRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var sources = await fixture.SeedSourcesAsync();
        var request = fixture.Request(sources.Content);

        // Act
        var result = await fixture.Handler().HandleAsync(Fixture.SystemContext(request),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionId { get; } = Uuid.CreateVersion4();
        public Uuid DescriptionVersionId { get; } = Uuid.CreateVersion4();
        public Uuid PackageId { get; } = Uuid.CreateVersion4();
        public Uuid AssertionId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorMemberId => RbacIds.Member(TenantId, UserId);
        public Uuid ReviewerMemberId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverMemberId { get; } = Uuid.CreateVersion4();
        public MutableTimeProvider Clock { get; } = new(Now.AddMinutes(8));
        public InMemoryEventStore PeriodEvents { get; } = new();
        public TypeIIPeriodReader Periods => new(Reader, PeriodEvents, ContentStore);
        public SystemDescriptionReader Descriptions => new(Reader, PeriodEvents, ContentStore);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public IImmutableContentStore ContentStore { get; }
        public AuditPackageReader Packages { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            ContentStore = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
            services.AddSingleton(ContentStore);
            services.AddScoped<AuditPackageReader>();
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Packages = _scope.ServiceProvider.GetRequiredService<AuditPackageReader>();
        }

        public RecordManagementAssertionHandler Handler() =>
            new(Executor, Reader, Packages, Periods, Clock, Descriptions);

        public async Task<SeededSources> SeedSourcesAsync(bool validatePackage = true,
            string wordingArtifactSource = "auditor")
        {
            var criteria = Reference("criteria");
            var control = Reference("control");
            var description = new SystemDescriptionAggregate(TenantId, ProgramId, DescriptionId);
            Assert.Null(description.Create(DescriptionVersionId,
                new SystemDescriptionScope("type_i", ExaminationId, BaselineId, null, null),
                DescriptionContent(), AuthorMemberId, "Assertion author", Now));
            Assert.Null(description.Review(1, ReviewerMemberId, "Independent reviewer",
                "approve", "Reviewed against the exact system scope.", Now.AddMinutes(1)));
            Assert.Null(description.Approve(1, ApproverMemberId, "CEO",
                "Approved the complete system description.", Now.AddMinutes(2)));

            var descriptionReference = new TypeIBaselineSourceReference("system_description",
                DescriptionId, DescriptionVersionId, 1, description.CurrentVersion!.ContentSha256);
            var baselineSources = new[]
            {
                new TypeIBaselineSourceReference(criteria.SourceType, criteria.RecordId,
                    criteria.VersionId, criteria.Version, criteria.ContentSha256),
                new TypeIBaselineSourceReference(control.SourceType, control.RecordId,
                    control.VersionId, control.Version, control.ContentSha256),
                descriptionReference,
            };
            var engagement = new ExaminationEngagement(TenantId, ProgramId, ExaminationId);
            var boundaryId = Uuid.CreateVersion4();
            Assert.Null(engagement.Create(new ExaminationEngagementContent("type_i",
                "Independent Audit LLP", new DateOnly(2026, 1, 1), null,
                ["security"], boundaryId, [AuthorMemberId]), AuthorMemberId,
                "Assertion author", Now));
            baselineSources =
            [
                new TypeIBaselineSourceReference("approved_boundary", boundaryId,
                    Uuid.CreateVersion4(), 1, Hash('b')),
                .. baselineSources,
            ];
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, ExaminationId,
                baselineSources);
            Assert.Null(engagement.FreezeBaseline(engagement.Revision, BaselineId, preview,
                "Confirmed the exact examination source population.", AuthorMemberId,
                "Assertion author", Now.AddMinutes(3)));
            Assert.Null(description.BindToExamination(1, ExaminationId, "type_i_baseline",
                BaselineId, ApproverMemberId, "CEO", Now.AddMinutes(4)));
            await DescriptionSeeds.WriteAsync(Writer, ContentStore, description);
            await Writer.SaveAsync(engagement, new RequestDispatchContext(RequestActor.System));

            var selected = new[] { criteria, control, Reference("system_description") with
            { RecordId = DescriptionId, VersionId = DescriptionVersionId, Version = 1,
                ContentSha256 = descriptionReference.ContentSha256 } };
            var selections = selected.Select(item => new AuditPackageSelectionEntry(item.SourceType,
                item.RecordId.ToString(), item.SourceType, item.RecordId, item.Version,
                "json", item.ContentSha256, 128, "management", item.VersionId)).ToArray();
            var package = new AuditPackageAggregate(TenantId, ProgramId, PackageId);
            Assert.Null(package.Create(new AuditPackageContent(ExaminationId, "type_i",
                "type_i_baseline", BaselineId, engagement.Baseline!.ContentSha256,
                "SOC 2 Type I examination", selections), AuthorMemberId, "Assertion author", Now));
            Assert.Null(package.RequestBuild(package.Revision, "assertion-package-build",
                AuthorMemberId, "Assertion author", Now.AddMinutes(5)));
            var manifest = package.Current!.Versions[0].Manifest;
            var attempt = package.Current.Versions[0].BuildAttempts.Single();
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                new AuditPackageBuildOutput(manifest.AuditPackageManifestId,
                    manifest.ManifestSha256, manifest.Entries, Uuid.CreateVersion4(), Hash('e'),
                    1024, null), null, "build-worker", Now.AddMinutes(6)));
            if (validatePackage)
                Assert.Null(package.RecordValidation(package.Revision, 1, "assertion-validation", [],
                    ReviewerMemberId, "Independent reviewer", Now.AddMinutes(7)));
            await PackageSeeds.WriteAsync(Writer, ContentStore, package.Current!);

            var wordingArtifactId = Uuid.CreateVersion4();
            var wordingArtifact = new EvidenceArtifact(TenantId, wordingArtifactId);
            Assert.True(wordingArtifact.Register(new EvidenceArtifactContent(
                    "Auditor-approved assertion wording", null, "document", wordingArtifactSource,
                    Now.AddMinutes(4), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                    "confidential"), Hash('f'), 512,
                ActorReference.ForMember(AuthorMemberId, "Assertion author"), Now.AddMinutes(4))
                .IsSuccess);
            Assert.Null(wordingArtifact.RecordInspection(EvidenceInspectionOutcome.Clean,
                Now.AddMinutes(4).AddSeconds(1)));
            await Writer.SaveAsync(wordingArtifact, new RequestDispatchContext(RequestActor.System));

            var content = new ManagementAssertionContent(ExaminationId, "type_i", BaselineId,
                engagement.Baseline.ContentSha256, DescriptionVersionId,
                description.CurrentVersion.ContentSha256, PackageId, 1,
                manifest.AuditPackageManifestId, manifest.ManifestSha256, "auditor",
                wordingArtifactId, "Management represents the exact reported system scope.",
                [criteria, control], [new ManagementAssertionSigner(ApproverMemberId, "ceo")]);
            return new SeededSources(content, criteria, control);
        }

        public RecordManagementAssertion Request(ManagementAssertionContent content) =>
            new(TenantId, ProgramId, ExaminationId, AssertionId, content);

        public Cntryl.Portia.RequestContext<RecordManagementAssertion> Context(
            RecordManagementAssertion request)
        {
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                    new Claim("email", "assertion-author@example.test")], "BdgrzSession"));
            return new Cntryl.Portia.RequestContext<RecordManagementAssertion>(request,
                new RequestDispatchContext(actor, Invocation()));
        }

        public static Cntryl.Portia.RequestContext<RecordManagementAssertion> SystemContext(
            RecordManagementAssertion request) => new(request,
            new RequestDispatchContext(RequestActor.System, Invocation()));

        static HttpInvocation Invocation() => new("POST",
            "/api/v1/tenants/test/programs/test/examinations/test/assertions/test",
            "/api/v1/tenants/test/programs/test/examinations/test/assertions/test",
            "record-management-assertion-test");

        static SystemDescriptionDraftContent DescriptionContent() => new("System Description",
        [
            new("services", "Services", "Hosted service", "complete", null),
            new("commitments_requirements", "Commitments and requirements", "Availability commitments", "complete", null),
            new("components_infrastructure_software", "Components and infrastructure", "Cloud platform", "complete", null),
            new("people", "People", "Security and operations teams", "complete", null),
            new("procedures", "Procedures", "Change and incident procedures", "complete", null),
            new("information_data_flows", "Information and data flows", "Customer data flows", "complete", null),
            new("system_boundary", "System boundary", "Production system", "complete", null),
            new("control_environment", "Control environment", "Approved controls", "complete", null),
            new("user_entity_controls", "User entity controls", "", "not_applicable", "No user entity controls apply"),
            new("subservice_organizations", "Subservice organizations", "Cloud provider", "complete", null),
            new("exclusions", "Exclusions", "Nonproduction sandbox excluded", "complete", null),
            new("relevant_changes", "Relevant changes", "", "not_applicable", "No changes in this period"),
        ], [], [], []);

        static ExaminationContentReference Reference(string sourceType) =>
            new(sourceType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Hash('c'));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FailingPackageContentStore(bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            throw new NotSupportedException("This fixture only reads package content.");

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("Temporarily unavailable package content.")
                : new ImmutableContentIntegrityException("Corrupt package content."));
    }

    sealed record SeededSources(ManagementAssertionContent Content,
        ExaminationContentReference Criteria, ExaminationContentReference Control);

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Set(DateTimeOffset value) => _utcNow = value;
    }

    static string Hash(char value) => new(value, 64);
}
