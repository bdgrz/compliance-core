using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class LinkEvidenceSupportHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldPersistExactSupportLinkGivenAvailableArtifactAndCurrentControlRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(EvidenceSupport.IdFor(request), result.Value.EvidenceSupportId);
        Assert.Equal(artifactId, result.Value.ArtifactId);
        Assert.Equal(2, result.Value.ArtifactRevision);
        Assert.Equal(1, support.Revision);
        Assert.Equal(artifactId, support.Current!.ArtifactId);
        Assert.Equal(controlId, support.Current.SupportedRecordId);
        Assert.Equal(1, support.Current.SupportedRecordRevision);
        Assert.Equal(2, support.Current.ArtifactRevision);
        Assert.Equal(new string('a', 64), support.Current.ArtifactContentSha256);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), support.Current.ActorMemberId);
    }

    [Fact]
    public async Task ShouldRejectUnavailableArtifactWithoutCreatingSupportGivenQuarantinedSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Malware);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleTargetRevisionWithoutCreatingSupportGivenLink()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 2);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public async Task ShouldRejectSupportPeriodOutsideArtifactCoverageGivenCurrentTarget()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public async Task ShouldAllowReusedEvidenceGivenCapturedCoverageIncludesRequestedPeriod()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean,
            new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31));
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.PeriodStart);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.PeriodEnd);
    }

    [Fact]
    public async Task ShouldRequireExactArtifactReadGrantGivenKnownArtifactIdentity()
    {
        // Arrange
        await using var fixture = new Fixture(allow: false);
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenSupportLink()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifactId = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifactId, controlId, 1);
        var context = new Cntryl.Portia.RequestContext<LinkEvidenceSupport>(request,
            new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/evidence/support", "/evidence/support", "support-test")));

        // Act
        var result = await fixture.Handler().HandleAsync(context, CancellationToken.None);
        var support = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalSupportGivenAcceptedRetryAfterTargetRevisionAdvances()
    {
        // Arrange
        await using var fixture = new Fixture();
        var artifact = await fixture.SeedArtifactAsync(EvidenceInspectionOutcome.Clean);
        var controlId = await fixture.SeedControlAsync();
        var request = fixture.Request(artifact, controlId, 1);
        var first = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var control = await fixture.Controls.RulesAsync(fixture.TenantId, fixture.ProgramId, controlId);
        Assert.Null(control.Revise(fixture.ProgramId, 1,
            new("Updated review", "New owner instruction", "Review access", "Quarterly review", ["Approval record"]),
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Evidence lead", Now));
        await ControlSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.Content, control, CancellationToken.None);

        // Act
        var result = await fixture.Handler().HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(first.Value, result.Value);
        var owner = await fixture.Reader.HydrateAsync(new EvidenceSupport(fixture.TenantId,
            fixture.ProgramId, EvidenceSupport.IdFor(request)));
        Assert.Equal(1UL, owner.CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IDomainEventReader Events { get; }
        public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());
        public ControlReader Controls => new(Reader, Events, Content);
        public EvidenceArtifactDownloadAccess Access { get; }

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "evidence@example.test")], "BdgrzSession"));

        public Fixture(bool allow = true)
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Events = _scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            Access = new(new FixedArtifactPermission(TenantId, UserId, allow));
        }

        public LinkEvidenceSupportHandler Handler() => new(Access,
            new EvidenceArtifactMetadataRead(Reader, Events), new EvidenceSupportTargetSource(Reader, Controls, EmptyOperations.Reader(Reader, Events), EmptyOperations.Evaluations(Reader, Events)),
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), TimeProvider.System, Reader);

        public LinkEvidenceSupport Request(Uuid artifactId, Uuid controlId, long revision) =>
            new(Uuid.CreateVersion4(), TenantId, ProgramId, artifactId, "control", controlId, revision,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review",
                "The artifact demonstrates the approved control design.", 0);

        public Cntryl.Portia.RequestContext<LinkEvidenceSupport> Context(
            LinkEvidenceSupport request) => new(request,
            new RequestDispatchContext(Actor, new HttpInvocation("POST", "/api/v1/evidence/support",
                "/api/v1/evidence/support", "evidence-support-test")));

        public async Task<Uuid> SeedArtifactAsync(EvidenceInspectionOutcome outcome,
            DateOnly? periodStart = null, DateOnly? periodEnd = null)
        {
            var artifactId = Uuid.CreateVersion4();
            var coveredFrom = periodStart ?? new DateOnly(2026, 1, 1);
            var coveredUntil = periodEnd ?? new DateOnly(2026, 12, 31);
            var artifact = new EvidenceArtifact(TenantId, artifactId);
            Assert.True(artifact.Register(new EvidenceArtifactContent("Audit evidence", null,
                    "document", "Manual upload", Now.AddMinutes(-1), coveredFrom,
                    coveredUntil, "restricted"), new string('a', 64), 128,
                ActorReference.ForMember(RbacIds.Member(TenantId, UserId), "Collector"),
                Now.AddMinutes(-1)).IsSuccess);
            Assert.Null(artifact.RecordInspection(outcome, Now));
            await Writer.SaveAsync(artifact, new RequestDispatchContext(RequestActor.System));
            return artifactId;
        }

        public async Task<Uuid> SeedControlAsync()
        {
            var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
            var control = new ControlRules(TenantId, controlId);
            Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-01",
                new ControlDraftContent("Access review", "Review access", "Management reviews access",
                    "The owner reviews the access list quarterly.", ["Dated approval record"]),
                RbacIds.Member(TenantId, UserId), "Evidence lead", Now).IsSuccess);
            await ControlSeeds.WriteAsync(Writer, Content, control, CancellationToken.None);
            return controlId;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedArtifactPermission(Uuid tenantId, Uuid userId, bool allow)
        : IAccessGrantScopePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allow);

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(allow, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid requestedTenantId, Uuid requestedUserId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allow &&
                requestedTenantId == tenantId && requestedUserId == userId &&
                memberId == RbacIds.Member(tenantId, userId) && scopes.Count == 1 &&
                scopes.Single() is
                {
                    Kind: AccessGrantScopeKind.SharedResource,
                    ResourceType: EvidenceArtifactResourceTypes.Artifact
                } scope &&
                scope.Id != Uuid.Empty && permission == RbacPermissions.EvidenceArtifactRead);

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(allow);
    }
}
