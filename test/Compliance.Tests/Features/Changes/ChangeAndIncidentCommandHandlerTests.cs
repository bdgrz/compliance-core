using System.Security.Claims;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Incidents;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Changes;

public sealed class ChangeAndIncidentCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 15, 0, 0, TimeSpan.Zero);
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid SystemId = Uuid.CreateVersion4();
    static readonly Uuid SystemVersionId = Uuid.CreateVersion4();
    static readonly Uuid EvidenceId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRecordReviseAndAssessSignificantChangeGivenCompleteDomainContent()
    {
        // Arrange
        await using var scenario = new Scenario();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var content = new SignificantChangeContent("change_control", "CHG-104", null,
            "Production identity update", "Replace the production identity integration.",
            new DateOnly(2026, 10, 18), null, RbacIds.Member(TenantId, author), [SystemId],
            "high", "restricted", [EvidenceId]);
        var revision = content with { Description = "Replace the production identity integration and reapprove its privileged access." };
        var assessment = new SignificantChangeImpactContent("significant",
            "The identity integration changes an in-scope key control.", false, false, true, false,
            ["control"], [new("system_instance", SystemId, SystemVersionId, 4)], [], [], [],
            [EvidenceId], false);

        // Act
        var recorded = await new RecordSignificantChangeHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new RecordSignificantChange(TenantId, ProgramId, changeId, content), author),
            CancellationToken.None);
        var revised = await new ReviseSignificantChangeHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new ReviseSignificantChange(TenantId, ProgramId, changeId, 1, revision,
                "Clarified the privileged access impact."), author), CancellationToken.None);
        var assessed = await new AssessSignificantChangeHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new AssessSignificantChange(TenantId, ProgramId, changeId, 2, assessment), reviewer),
            CancellationToken.None);
        var staleAssessment = await new AssessSignificantChangeHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new AssessSignificantChange(TenantId, ProgramId, changeId, 1,
                assessment with { Rationale = "A conflicting stale assessment." }), reviewer), CancellationToken.None);
        var aggregate = await scenario.Reader.HydrateAsync(new SignificantChange(TenantId, ProgramId, changeId));

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(assessed.IsSuccess, assessed.Error?.Message);
        Assert.False(staleAssessment.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleAssessment.Error!.Kind);
        Assert.Equal("assessed", aggregate.Current!.Status);
        Assert.Equal(revision.Description, aggregate.Current.Current!.Content.Description);
        Assert.Equal(2, aggregate.Current.CurrentVersionNumber);
        Assert.Equal(RbacIds.Member(TenantId, reviewer), aggregate.Current.CurrentAssessment!.AssessedByMemberId);
        Assert.Equal("significant", aggregate.Current.CurrentAssessment.Significance);
    }

    [Fact]
    public async Task ShouldRecordReviseAssessAndResolveIncidentGivenCompleteChronologyAndEvidence()
    {
        // Arrange
        await using var scenario = new Scenario();
        var reporter = Uuid.CreateVersion4();
        var reviser = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var resolver = Uuid.CreateVersion4();
        var incidentId = Uuid.CreateVersion4();
        var occurredAt = Now.AddMinutes(-30);
        var detectedAt = Now.AddMinutes(-20);
        var content = new ComplianceIncidentContent("security_monitor", "INC-44", null, null,
            "Production identity alert", "An identity integration exposed customer data.", "security",
            "critical", occurredAt, detectedAt, RbacIds.Member(TenantId, reporter), true, false,
            [SystemId], "restricted", [EvidenceId]);
        var revision = content with { Description = "An identity integration exposed customer data through a misconfigured production policy." };
        var assessment = new ComplianceIncidentImpactAssessmentContent("significant",
            "Customer data was affected by a security incident.", true, true, true, true,
            ["scope", "control", "risk", "vendor", "evidence"],
            [new("system_instance", SystemId, SystemVersionId, 4)], [], [EvidenceId], false,
            true, false);
        var resolution = new ComplianceIncidentResolutionContent(
            "The policy was corrected and the affected identity integration was rotated.", Now,
            [EvidenceId], []);

        // Act
        var recorded = await new RecordComplianceIncidentHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new RecordComplianceIncident(TenantId, ProgramId, incidentId, content), reporter),
            CancellationToken.None);
        var revised = await new ReviseComplianceIncidentHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new ReviseComplianceIncident(TenantId, ProgramId, incidentId, 1, revision,
                "Added the production policy detail."), reviser), CancellationToken.None);
        var assessed = await new AssessComplianceIncidentHandler(scenario.Executor, scenario.Clock).HandleAsync(
            Scenario.Context(new AssessComplianceIncident(TenantId, ProgramId, incidentId, 2, assessment), assessor),
            CancellationToken.None);
        var resolved = await new RecordComplianceIncidentResolutionHandler(scenario.Executor, scenario.Clock)
            .HandleAsync(Scenario.Context(new RecordComplianceIncidentResolution(TenantId, ProgramId, incidentId,
                3, resolution), resolver), CancellationToken.None);
        var aggregate = await scenario.Reader.HydrateAsync(new ComplianceIncident(TenantId, ProgramId, incidentId));

        // Assert
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.True(assessed.IsSuccess, assessed.Error?.Message);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal("resolved", aggregate.Current!.Status);
        Assert.Equal(revision.Description, aggregate.Current.Current!.Content.Description);
        Assert.Equal(2, aggregate.Current.CurrentVersionNumber);
        Assert.Equal(RbacIds.Member(TenantId, assessor), aggregate.Current.CurrentAssessment!.AssessedByMemberId);
        Assert.Equal(RbacIds.Member(TenantId, resolver), aggregate.Current.Resolution!.ResolvedByMemberId);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _services;
        readonly AsyncServiceScope _scope;

        public Scenario()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _services.CreateAsyncScope();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);
        public IAggregateExecutor Executor { get; }
        public IAggregateReader Reader { get; }

        public static RequestContext<TRequest> Context<TRequest>(TRequest request, Uuid userId)
            where TRequest : IRequestBase => new(request, new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("name", "Test user")],
                "UnitTest")));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
