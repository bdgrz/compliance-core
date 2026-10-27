using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Handlers;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackageRules;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class AuditPackageCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldApproveAndRecordDeliveryForExactValidatedPackageGivenReceipt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedBuiltAndValidatedPackageAsync();
        const string recipient = "auditor@example.test";
        var approval = new ApproveAuditPackageDisclosure(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), recipient, "Type I examination", "Approved exact validated archive.");
        var approve = fixture.ApproveDisclosureHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, approval), CancellationToken.None);

        // Act
        var approved = await approve;
        var delivery = await fixture.RecordDeliveryHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, new RecordAuditPackageDelivery(fixture.TenantId, fixture.ProgramId, packageId, 1, recipient, Now.AddMinutes(4), "mail-receipt-1", AuditPackageCommandIdentity.Delivery(packageId, 1, recipient, "mail-receipt-1"), await fixture.ManifestRevisionAsync(packageId))),
            CancellationToken.None);
        await fixture.MaterializeDeliveriesAsync(packageId);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(delivery.IsSuccess, delivery.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("delivered", version.Status);
        Assert.Equal(recipient, Assert.Single(version.DisclosureApprovals).Recipient);
        var receipt = Assert.Single(version.Deliveries);
        Assert.Equal("mail-receipt-1", receipt.DeliveryReference);
        Assert.Equal(version.Manifest.ManifestSha256, receipt.ManifestSha256);
    }

    [Fact]
    public async Task ShouldRejectDeliveryWithoutExactRecipientApprovalGivenValidatedPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedBuiltAndValidatedPackageAsync();

        // Act
        var result = await fixture.RecordDeliveryHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, new RecordAuditPackageDelivery(fixture.TenantId, fixture.ProgramId, packageId, 1, "unapproved@example.test", Now.AddMinutes(4), "mail-receipt-2", AuditPackageCommandIdentity.Delivery(packageId, 1, "unapproved@example.test", "mail-receipt-2"), await fixture.ManifestRevisionAsync(packageId))), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Current!.Versions[0].Deliveries);
        Assert.Equal(4, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenDisclosureApproval()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new ApproveAuditPackageDisclosure(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), 1, await fixture.ManifestRevisionAsync(Uuid.CreateVersion4()), "auditor@example.test", "Type I examination", "Approved exact validated archive.");

        // Act
        var result = await fixture.ApproveDisclosureHandler().HandleAsync(
            Fixture.SystemContext(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldQueueBuildForExactExistingPackageGivenPersonalActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedDraftPackageAsync();
        var command = new RequestAuditPackageBuild(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, packageId, "type_i", "build-key-1", await fixture.ManifestRevisionAsync(packageId), 1);

        // Act
        var result = await fixture.RequestBuildHandler().HandleAsync(Fixture.Context(
            fixture.LeadUserId, command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var view = saved.Current!;
        Assert.Equal("building", view.Status);
        var attempt = Assert.Single(view.Versions[0].BuildAttempts);
        Assert.Equal("build-key-1", attempt.BuildKey);
        Assert.Equal("requested", attempt.Status);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.LeadUserId),
            attempt.RequestedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenBuildRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var command = new RequestAuditPackageBuild(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, Uuid.CreateVersion4(), "type_i", "build-key-2", await fixture.ManifestRevisionAsync(Uuid.CreateVersion4()), 1);

        // Act
        var result = await fixture.RequestBuildHandler().HandleAsync(
            Fixture.SystemContext(command), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectBuildRequestForDifferentExaminationGivenExistingPackage()
    {
        // Arrange
        await using var fixture = new Fixture();
        var packageId = await fixture.SeedDraftPackageAsync();
        var command = new RequestAuditPackageBuild(fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), packageId, "type_i", "build-key-3", await fixture.ManifestRevisionAsync(packageId), 1);

        // Act
        var result = await fixture.RequestBuildHandler().HandleAsync(Fixture.Context(
            fixture.LeadUserId, command), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("draft", saved.Current!.Status);
        Assert.Empty(saved.Current.Versions[0].BuildAttempts);
    }

    [Fact]
    public async Task ShouldOverrideExactBlockingFindingGivenIndependentExecutiveActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (packageId, findingId) = await fixture.SeedBuiltAndBlockedPackageAsync();

        // Act
        var result = await fixture.ApproveValidationOverrideHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, new ApproveAuditPackageValidationOverride(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, packageId, 1, await fixture.ManifestRevisionAsync(packageId), findingId.ToString(), "The scoped exception is accepted and disclosed to the auditor.")),
            CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("validated_with_overrides", version.Status);
        var validation = Assert.Single(version.Validations);
        Assert.Equal("passed_with_overrides", validation.Status);
        Assert.Equal(findingId, Assert.Single(validation.Overrides).FindingId);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        static readonly Uuid ArchiveId = Uuid.CreateVersion4();
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddMinutes(4));
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid LeadUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore PeriodEvents { get; } = new();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        readonly string _artifactRoot = Path.Combine(Path.GetTempPath(), $"package-command-{Guid.NewGuid():N}");
        public IArtifactContentStore ArtifactStore { get; }
        public IAggregateExecutor Executor { get; }
        public IImmutableContentStore ContentStore { get; } =
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        public AuditPackageReader Packages => new(Reader, ContentStore, PeriodEvents);
        public AuditPackageRootOperation RootOperation => new(Reader, Packages,
            new NativeOperationExecutor(Reader, Executor, ContentStore), ContentStore, _clock);
        public async Task<AuditPackageRules> ReadPackageAsync(Uuid id) =>
            new(TenantId, ProgramId, id, (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).View);
        public async Task<long> ManifestRevisionAsync(Uuid id) =>
            (await Packages.ReadAsync(TenantId, ProgramId, id, CancellationToken.None)).Owner.Revision;


        public Fixture()
        {
            ArtifactStore = new LocalArtifactContentStore(new ArtifactContentStoreOptions(
                _artifactRoot, null, ArtifactContentLimits.MaxContentLength), _clock);
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(PeriodEvents);
            services.AddPortia();
            PackageSeeds.Configure(services, PeriodEvents);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public ApproveAuditPackageDisclosureHandler ApproveDisclosureHandler() =>
            new(RootOperation, ContentStore);

        public RecordAuditPackageDeliveryHandler RecordDeliveryHandler() =>
            new(RootOperation, ContentStore, ArtifactStore);

        public ApproveAuditPackageValidationOverrideHandler ApproveValidationOverrideHandler() =>
            new(RootOperation, ContentStore);

        public RequestAuditPackageBuildHandler RequestBuildHandler() =>
            new(RootOperation, ContentStore);

        public async Task MaterializeDeliveriesAsync(Uuid packageId)
        {
            var owner = (await Packages.ReadAsync(TenantId, ProgramId, packageId, CancellationToken.None)).Owner;
            foreach (var fact in (await AuditPackageManifestHistory.ReadAsync(PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>())
            {
                var command = new MaterializeRecordedAuditPackageDelivery(
                    EffectIdentity.CommandId(fact.Metadata.EventId, $"{AuditPackageDeliveryRecordingProcess.Name}:{nameof(MaterializeRecordedAuditPackageDelivery)}", new AuditPackageDelivery(TenantId, packageId, fact.PackageVersion, fact.Operation.LogicalId).Stream.ToString(), fact.Operation.Episode),
                    NativeFactReference.Of(owner.Stream, fact), new AuditPackageDelivery(TenantId, packageId,
                        fact.PackageVersion, fact.Operation.LogicalId).Stream, fact.Operation.LogicalId,
                    fact.PackageVersion, null, fact.Intent, packageId, fact.Operation.LogicalId, fact.PackageVersion);
                var handler = new MaterializeRecordedAuditPackageDeliveryHandler(
                    new AuditPackageSourceVerifier(Reader, ContentStore, PeriodEvents), Reader,
                    new Bdgrz.Compliance.Features.Processes.ReceiverAppend(Executor), ContentStore, _clock, PeriodEvents);
                var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<MaterializeRecordedAuditPackageDelivery>(
                    command, new RequestDispatchContext(RequestActor.System)), CancellationToken.None);
                Assert.True(result.IsSuccess, result.Error?.Message);
            }
        }

        public async Task<Uuid> SeedDraftPackageAsync()
        {
            var packageId = Uuid.CreateVersion4();
            var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
            Assert.Null(package.Create(Content(packageId), RbacIds.Member(TenantId, LeadUserId),
                "Lead", Now));
            await PackageSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, package.Current!);
            return packageId;
        }

        public async Task<Uuid> SeedBuiltAndValidatedPackageAsync()
        {
            var packageId = Uuid.CreateVersion4();
            var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
            var leadMember = RbacIds.Member(TenantId, LeadUserId);
            var reviewerMember = RbacIds.Member(TenantId, ReviewerUserId);
            Assert.Null(package.Create(Content(packageId), leadMember, "Lead", Now));
            Assert.Null(package.RequestBuild(package.Revision, "build-1", leadMember,
                "Lead", Now.AddMinutes(1)));
            var manifest = package.Current!.Versions[0].Manifest;
            var attempt = Assert.Single(package.Current.Versions[0].BuildAttempts);
            await StoreSourceBytesAsync();
            var built = await new AuditPackageArchiveBuilder(ArtifactStore).BuildAsync(TenantId, manifest,
                CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                built.Value!, null, "build-worker", Now.AddMinutes(2)));
            Assert.Null(package.RecordValidation(package.Revision, 1, "validation-1", [],
                reviewerMember, "Independent reviewer", Now.AddMinutes(3)));
            await PackageSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, package.Current!);
            return packageId;
        }

        public async Task<(Uuid PackageId, Uuid FindingId)> SeedBuiltAndBlockedPackageAsync()
        {
            var packageId = Uuid.CreateVersion4();
            var package = new AuditPackageAggregate(TenantId, ProgramId, packageId);
            var leadMember = RbacIds.Member(TenantId, LeadUserId);
            var reviewerMember = RbacIds.Member(TenantId, ReviewerUserId);
            Assert.Null(package.Create(Content(packageId), leadMember, "Lead", Now));
            Assert.Null(package.RequestBuild(package.Revision, "blocked-build-1", leadMember,
                "Lead", Now.AddMinutes(1)));
            var manifest = package.Current!.Versions[0].Manifest;
            var attempt = Assert.Single(package.Current.Versions[0].BuildAttempts);
            await StoreSourceBytesAsync();
            var built = await new AuditPackageArchiveBuilder(ArtifactStore).BuildAsync(TenantId, manifest,
                CancellationToken.None);
            Assert.True(built.IsSuccess, built.Error?.Message);
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                built.Value!, null, "build-worker", Now.AddMinutes(2)));
            var finding = new AuditPackageValidationFinding(Uuid.CreateVersion4(), "stale_source",
                "error", true, true, "A selected governed source requires an authorized override.");
            Assert.Null(package.RecordValidation(package.Revision, 1, "blocked-validation-1",
                [finding], reviewerMember, "Independent reviewer", Now.AddMinutes(3)));
            await PackageSeeds.WriteAsync(Reader, Writer, PeriodEvents, ContentStore, package.Current!);
            return (packageId, finding.FindingId);
        }

        static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)));
        async Task StoreSourceBytesAsync()
        {
            await ArtifactStore.StoreIfAbsentAsync(TenantId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes("control-17")));
            await ArtifactStore.StoreIfAbsentAsync(TenantId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes("artifact-9")));
        }

        AuditPackageContent Content(Uuid packageId) => new(ExaminationId, "type_i",
            "type_i_baseline", BaselineId, new string('a', 64), "Type I package", [
                new("control_matrix", "control-17", "control", Uuid.CreateVersion5(packageId,
                    "control-17"), 3, "csv", Hash("control-17"), 10, "product_calculated"),
                new("evidence_index", "artifact-9", "evidence_artifact", Uuid.CreateVersion5(
                    packageId, "artifact-9"), 2, "csv", Hash("artifact-9"), 10, "management"),
            ]);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/audit-packages/command",
                "/api/v1/audit-packages/command", "audit-package-test")));

        public static Cntryl.Portia.RequestContext<T> SystemContext<T>(T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(RequestActor.System,
            new HttpInvocation("POST", "/api/v1/audit-packages/command",
                "/api/v1/audit-packages/command", "audit-package-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "audit-package@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            if (Directory.Exists(_artifactRoot))
                Directory.Delete(_artifactRoot, recursive: true);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
