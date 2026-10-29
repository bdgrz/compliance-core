using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Services;
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
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Handlers;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Retention.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Retention.Tests;

public sealed class ArtifactDisclosureHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldApproveExactAvailableClassifiedArtifactGivenCurrentRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.ArtifactAsync();
        var request = new ApproveArtifactDisclosure(fixture.TenantId, Uuid.CreateVersion4(), artifactId,
            "External auditor", "Type I report", 2, "Approved evidence package");
        var handler = new ApproveArtifactDisclosureHandler(fixture.Access, fixture.Executor,
            fixture.Reader, fixture.Clock);

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new ArtifactDisclosure(fixture.TenantId,
            request.DisclosureId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("approved", retained.Current!.Status);
        Assert.Equal(artifactId, retained.Current.ArtifactId);
        Assert.Equal("restricted", retained.Current.HandlingClass);
        Assert.Equal(1, retained.Current.HandlingPolicyRevision);
        Assert.Equal(2UL, retained.Current.ApprovedSourcePosition);
    }

    [Theory]
    [InlineData("stale_revision")]
    [InlineData("not_available")]
    [InlineData("missing_policy")]
    public async Task ShouldAppendNoDisclosureGivenUnapprovedArtifactSource(string change)
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.ArtifactAsync(available: change != "not_available",
            assignPolicy: change != "missing_policy");
        var revision = change == "stale_revision" ? 1 : 2;
        var request = new ApproveArtifactDisclosure(fixture.TenantId, Uuid.CreateVersion4(), artifactId,
            "External auditor", "Type I report", revision, "Approved evidence package");
        var handler = new ApproveArtifactDisclosureHandler(fixture.Access, fixture.Executor,
            fixture.Reader, fixture.Clock);

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new ArtifactDisclosure(fixture.TenantId,
            request.DisclosureId));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, retained.Revision);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldDeliverAndRevokeAtExpectedDisclosureRevisionGivenCurrentSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.ArtifactAsync();
        var disclosureId = Uuid.CreateVersion4();
        var approve = new ApproveArtifactDisclosure(fixture.TenantId, disclosureId, artifactId,
            "External auditor", "Type I report", 2, "Approved evidence package");
        Assert.True((await new ApproveArtifactDisclosureHandler(fixture.Access, fixture.Executor, fixture.Reader,
            fixture.Clock).HandleAsync(fixture.Context(approve), CancellationToken.None)).IsSuccess);
        var deliver = new RecordArtifactDisclosureDelivery(fixture.TenantId, disclosureId, artifactId,
            1, Now, "receipt-2026-10");
        var deliveryHandler = new RecordArtifactDisclosureDeliveryHandler(fixture.Access, fixture.Executor,
            fixture.Reader, fixture.Clock);

        // Act
        var delivered = await deliveryHandler.HandleAsync(fixture.Context(deliver), CancellationToken.None);
        var retry = await deliveryHandler.HandleAsync(fixture.Context(deliver), CancellationToken.None);
        var staleRevoke = await new RevokeArtifactDisclosureHandler(fixture.Access, fixture.Executor, fixture.Reader,
            fixture.Clock).HandleAsync(fixture.Context(new RevokeArtifactDisclosure(fixture.TenantId,
                disclosureId, artifactId, 1, "External access ended")), CancellationToken.None);
        var revoked = await new RevokeArtifactDisclosureHandler(fixture.Access, fixture.Executor, fixture.Reader,
            fixture.Clock).HandleAsync(fixture.Context(new RevokeArtifactDisclosure(fixture.TenantId,
                disclosureId, artifactId, 2, "External access ended")), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new ArtifactDisclosure(fixture.TenantId, disclosureId));

        // Assert
        Assert.True(delivered.IsSuccess, delivered.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, staleRevoke.Error?.Kind);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Equal("revoked", retained.Current!.Status);
        Assert.Equal("receipt-2026-10", retained.Current.Delivery!.DeliveryReference);
        Assert.False(retained.Current.DownloadedCopiesRecallable);
        Assert.Equal(3, retained.Revision);
        Assert.Equal(3UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenDisclosureDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.ArtifactAsync();
        var request = new ApproveArtifactDisclosure(fixture.TenantId, Uuid.CreateVersion4(), artifactId,
            "External auditor", "Type I report", 2, "Approved evidence package");
        var context = new Cntryl.Portia.RequestContext<ApproveArtifactDisclosure>(request,
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("POST", "/test", "/test", "test")));

        // Act
        var result = await new ApproveArtifactDisclosureHandler(fixture.Access, fixture.Executor, fixture.Reader,
            fixture.Clock).HandleAsync(context, CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new ArtifactDisclosure(fixture.TenantId,
            request.DisclosureId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldDenyDisclosureGivenArtifactReadWithoutSeparateDisclosureGrant()
    {
        // Arrange
        await using var fixture = new Fixture(disclosureAllowed: false);
        var artifactId = await fixture.ArtifactAsync();
        var request = new ApproveArtifactDisclosure(fixture.TenantId, Uuid.CreateVersion4(), artifactId,
            "External auditor", "Type I report", 2, "Approved evidence package");

        // Act
        var result = await new ApproveArtifactDisclosureHandler(fixture.Access, fixture.Executor,
            fixture.Reader, fixture.Clock).HandleAsync(fixture.Context(request), CancellationToken.None);
        var retained = await fixture.Reader.HydrateAsync(new ArtifactDisclosure(fixture.TenantId,
            request.DisclosureId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly Uuid _userId = Uuid.CreateVersion4();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);
        public ArtifactDisclosureAccess Access { get; }
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", _userId.ToString()),
                new Claim("name", "Compliance lead")], "BdgrzSession"));

        public Fixture(bool disclosureAllowed = true)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Access = new(new AllowDisclosurePermission(disclosureAllowed));
        }

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/api/v1/tenants/disclosures",
                "/api/v1/tenants/disclosures", "artifact-disclosure-test")));

        public async Task<Uuid> ArtifactAsync(bool available = true, bool assignPolicy = true)
        {
            var artifactId = Uuid.CreateVersion4();
            var artifact = new EvidenceArtifact(TenantId, artifactId);
            var content = new EvidenceArtifactContent("SOC 2 evidence", null, "export", "manual", Now,
                new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), "restricted");
            Assert.True(artifact.Register(content, Sha, 4096,
                ActorReference.ForMember(RbacIds.Member(TenantId, _userId), "Collector"), Now).IsSuccess);
            Assert.Null(artifact.RecordInspection(available ? EvidenceInspectionOutcome.Clean :
                EvidenceInspectionOutcome.Malware, Now.AddMinutes(1)));
            await _scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(artifact,
                Context(new GetArtifactRetention(TenantId, "evidence_artifact", artifactId)));
            if (assignPolicy)
            {
                var source = new ArtifactRetentionSource(TenantId, "evidence_artifact", artifactId, Sha);
                var policy = new EvidenceHandlingPolicy(TenantId, artifactId);
                Assert.True(policy.Assign(new(source, 2, 4096, available ? "available" : "quarantined",
                        "restricted"), 0, "restricted", EvidenceHandlingPolicy.CurrentPolicyVersion,
                    "Reviewed restricted classification", ActorReference.ForMember(
                        RbacIds.Member(TenantId, _userId), "Compliance lead"), Now.AddMinutes(2)).IsSuccess);
                await _scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(policy,
                    Context(new AssignArtifactHandlingPolicy(TenantId, artifactId, 0, "restricted",
                        EvidenceHandlingPolicy.CurrentPolicyVersion, "Reviewed restricted classification")));
            }
            return artifactId;
        }

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

    sealed class AllowDisclosurePermission(bool disclosureAllowed) : IAccessGrantScopePermissionAuthorizer
    {
        readonly bool _disclosureAllowed = disclosureAllowed;

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(_disclosureAllowed);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(_disclosureAllowed, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            IReadOnlyCollection<AccessGrantScope> scopes, string permission, CancellationToken ct = default)
        {
            if (tenantId == Uuid.Empty || userId == Uuid.Empty || memberId == Uuid.Empty || scopes.Count != 1)
                return ValueTask.FromResult(false);
            var scope = scopes.Single();
            var allowed = scope.Kind == AccessGrantScopeKind.SharedResource && scope.Id != Uuid.Empty &&
                scope.ResourceType == EvidenceArtifactResourceTypes.Artifact && permission switch
                {
                    RbacPermissions.EvidenceArtifactRead => true,
                    RbacPermissions.EvidenceArtifactDisclose => _disclosureAllowed,
                    _ => false,
                };
            return ValueTask.FromResult(allowed);
        }

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(false);
    }
}
