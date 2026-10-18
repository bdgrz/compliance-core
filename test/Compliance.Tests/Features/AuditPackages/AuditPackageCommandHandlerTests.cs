using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPackages;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPackageAggregate = Bdgrz.Compliance.Features.AuditPackages.AuditPackage;

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
        var approval = new ApproveAuditPackageDisclosure(fixture.TenantId, fixture.ProgramId,
            packageId, 1, recipient, "Type I examination", "Approved exact validated archive.");
        var approve = fixture.ApproveDisclosureHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, approval), CancellationToken.None);

        // Act
        var approved = await approve;
        var delivery = await fixture.RecordDeliveryHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, new RecordAuditPackageDelivery(fixture.TenantId,
                fixture.ProgramId, packageId, 1, recipient, Now.AddMinutes(4), "mail-receipt-1")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
            fixture.ApproverUserId, new RecordAuditPackageDelivery(fixture.TenantId,
                fixture.ProgramId, packageId, 1, "unapproved@example.test", Now.AddMinutes(4),
                "mail-receipt-2")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

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
        var request = new ApproveAuditPackageDisclosure(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion4(), 1, "auditor@example.test", "Type I examination",
            "Approved exact validated archive.");

        // Act
        var result = await fixture.ApproveDisclosureHandler().HandleAsync(
            Fixture.SystemContext(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldOverrideExactBlockingFindingGivenIndependentExecutiveActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (packageId, findingId) = await fixture.SeedBuiltAndBlockedPackageAsync();

        // Act
        var result = await fixture.ApproveValidationOverrideHandler().HandleAsync(Fixture.Context(
            fixture.ApproverUserId, new ApproveAuditPackageValidationOverride(fixture.TenantId,
                fixture.ProgramId, packageId, 1, findingId.ToString(),
                "The scoped exception is accepted and disclosed to the auditor.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPackageAggregate(fixture.TenantId,
            fixture.ProgramId, packageId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var version = Assert.Single(saved.Current!.Versions);
        Assert.Equal("validated_with_overrides", version.Status);
        var validation = Assert.Single(version.Validations);
        Assert.Equal("passed_with_overrides", validation.Status);
        Assert.Equal(findingId, Assert.Single(validation.Overrides).FindingId);
    }

    sealed class Fixture : IAsyncDisposable
    {
        static readonly Uuid ArchiveId = Uuid.CreateVersion4();
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now);
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid LeadUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
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
            new(Executor, _clock);

        public RecordAuditPackageDeliveryHandler RecordDeliveryHandler() =>
            new(Executor);

        public ApproveAuditPackageValidationOverrideHandler ApproveValidationOverrideHandler() =>
            new(Executor, _clock);

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
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                new AuditPackageBuildOutput(manifest.AuditPackageManifestId,
                    manifest.ManifestSha256, manifest.Entries, ArchiveId,
                    new string('f', 64), 4096, null), null, "build-worker", Now.AddMinutes(2)));
            Assert.Null(package.RecordValidation(package.Revision, 1, "validation-1", [],
                reviewerMember, "Independent reviewer", Now.AddMinutes(3)));
            await Writer.SaveAsync(package, new RequestDispatchContext(RequestActor.System));
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
            Assert.Null(package.RecordBuildResult(package.Revision, attempt.BuildAttemptId,
                new AuditPackageBuildOutput(manifest.AuditPackageManifestId,
                    manifest.ManifestSha256, manifest.Entries, ArchiveId,
                    new string('f', 64), 4096, null), null, "build-worker", Now.AddMinutes(2)));
            var finding = new AuditPackageValidationFinding(Uuid.CreateVersion4(), "stale_source",
                "error", true, true, "A selected governed source requires an authorized override.");
            Assert.Null(package.RecordValidation(package.Revision, 1, "blocked-validation-1",
                [finding], reviewerMember, "Independent reviewer", Now.AddMinutes(3)));
            await Writer.SaveAsync(package, new RequestDispatchContext(RequestActor.System));
            return (packageId, finding.FindingId);
        }

        AuditPackageContent Content(Uuid packageId) => new(ExaminationId, "type_i",
            "type_i_baseline", BaselineId, new string('a', 64), "Type I package", [
                new("control_matrix", "control-17", "control", Uuid.CreateVersion5(packageId,
                    "control-17"), 3, "csv", new string('b', 64), 512, "product_calculated"),
                new("evidence_index", "artifact-9", "evidence_artifact", Uuid.CreateVersion5(
                    packageId, "artifact-9"), 2, "csv", new string('c', 64), 1024, "management"),
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
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
