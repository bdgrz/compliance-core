using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Fitz;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Controls.Queries;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AttestManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    static readonly ClaimsPrincipal Actor = ProgramManagementServices.Actor(User);

    [Theory]
    [InlineData("client_personnel", "control", false)]
    [InlineData("guest", "control", false)]
    [InlineData("client_personnel", "policy", false)]
    [InlineData("client_personnel", "evidence", false)]
    [InlineData("client_personnel", "attest", false)]
    [InlineData("client_personnel", "review", false)]
    [InlineData("client_personnel", "evaluation", false)]
    [InlineData("client_personnel", "corrective", false)]
    [InlineData("client_personnel", "control", true)]
    public async Task ShouldDenyManagementWriteGivenCanonicalActualAttestHistoryAndCurrentClientGrant(
        string affiliation, string operation, bool revoked)
    {
        // Arrange
        await using var provider = Compose(affiliation);
        await SeedProgramAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var control = Uuid.CreateVersion4();
        var occurrence = Uuid.CreateVersion4();

        // Act
        var error = operation switch
        {
            "control" => (await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1",
                Content), Actor)).Error,
            "policy" => (await bus.SendAsync(new CreatePolicyDraft(Policy.IdFor(Tenant, Program, "POL-1"), Tenant, Program, "POL-1",
                new PolicyContent("Policy", "Purpose", "core_security", null, 12, "Body", null, null, null)), Actor)).Error,
            "evidence" => (await bus.SendAsync(new OpenEvidenceRequest(Uuid.CreateVersion4(), Tenant, Program, "Evidence", "Instructions",
                RbacIds.Member(Tenant, User), new DateOnly(2027, 1, 1)), Actor)).Error,
            "attest" => (await bus.SendAsync(new AttestControlOccurrence(Uuid.CreateVersion4(), Tenant, Program, control, occurrence,
                1, "performed", Now, null, null, null, null, []), Actor)).Error,
            "review" => (await bus.SendAsync(new ReviewControlOccurrence(Uuid.CreateVersion4(), Tenant, Program, control, occurrence,
                1, Uuid.CreateVersion4(), "accept", "Review rationale"), Actor)).Error,
            "evaluation" => (await bus.SendAsync(new StartControlEvaluation(Uuid.CreateVersion4(), Tenant, Program, control,
                Uuid.CreateVersion4()), Actor)).Error,
            "corrective" => (await bus.SendAsync(new CompleteCorrectiveAction(Uuid.CreateVersion4(), Tenant, Program,
                Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), "Completed work", []), Actor)).Error,
            _ => throw new InvalidOperationException()
        };

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains(affiliation == "guest" ? "Guest members" : "Attest",
            error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("list")]
    public async Task ShouldAllowEvaluationPlanVersionReadGivenActualAttestHistory(string operation)
    {
        // Arrange
        await using var provider = Compose("client_personnel");
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var control = await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);
        Assert.True(control.IsSuccess, control.Error?.Message);
        var version = Uuid.CreateVersion4();
        await EvaluationPlanSeeds.WriteAsync(provider, new ControlEvaluationPlanVersionView(Tenant, Program,
            control.Value!.ControlId, Uuid.CreateVersion4(), version, 1, "Review access",
            [new EvaluationProcedureStep("design", "inspection",
                [new EvaluationInspectedItem("artifact", "synthetic-artifact", "1")], "Access is reviewed")],
            true, RbacIds.Member(Tenant, User), ActorReference.ForMember(RbacIds.Member(Tenant, User), "Client author"), Now));
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, true);

        // Act
        if (operation == "get")
        {
            var result = await bus.SendAsync(new GetControlEvaluationPlanVersion(Tenant, Program,
                control.Value.ControlId, version), Actor);
            // Assert
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(version, result.Value!.PlanVersionId);
        }
        else
        {
            var result = await bus.SendAsync(new ListControlEvaluationPlanVersions(Tenant, Program,
                control.Value.ControlId), Actor);
            // Assert
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(version, Assert.Single(result.Value!.Items).PlanVersionId);
        }
    }

    [Theory]
    [InlineData("http")]
    [InlineData("mcp")]
    public async Task ShouldDenyManagementWriteGivenNativeTransportAndPermanentAttestHistory(string transport)
    {
        // Arrange
        await using var provider = Compose("client_personnel");
        await SeedProgramAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, true);
        await using var scope = provider.CreateAsyncScope();
        RequestInvocation invocation = transport == "http"
            ? new HttpInvocation("POST", "/synthetic/control-drafts", "/synthetic/control-drafts", "synthetic")
            : new McpInvocation("bdgrz.control.draft.create");

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content),
            new RequestDispatchContext(Actor, invocation), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("client_personnel")]
    [InlineData("guest")]
    public async Task ShouldLimitSharedControlReadToClientPersonnelGivenHistoricalAttestIdentity(
        string affiliation)
    {
        // Arrange
        await using var provider = Compose(affiliation);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        await SeedProgramAsync(provider);
        var controlId = ControlDraft.IdFor(Tenant, Program, "AC-1");
        if (affiliation == "client_personnel")
        {
            var control = await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);
            Assert.True(control.IsSuccess, control.Error?.Message);
            controlId = control.Value!.ControlId;
        }
        else
        {
            await ProgramManagementServices.SeedAsync(provider, new ControlRules(Tenant, controlId), draft =>
            {
                Assert.True(draft.Create(Program, Uuid.CreateVersion4(), "AC-1", Content,
                    RbacIds.Member(Tenant, User), "Client author", Now).IsSuccess);
                return Result.Success;
            });
        }
        var projection = scope.ServiceProvider.GetRequiredService<IControlDraftDirectoryProjection>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var cursor = EventCursor.Start;
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzControlDraftDirectoryV2.ProjectorName, EventStreamPattern.ForPattern(
                             Tenant.ToString(), "controls")), ProjectionCheckpoint.Start)))
        {
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                               Tenant.ToString(), "controls", controlId.ToString()), cursor, CancellationToken.None))
            {
                await projection.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, true);

        // Act
        var read = await bus.SendAsync(new GetControlDraft(Tenant, Program, controlId), Actor);

        // Assert
        if (affiliation == "guest")
            Assert.Equal(RequestErrorKind.Forbidden, read.Error?.Kind);
        else
        {
            Assert.True(read.IsSuccess, read.Error?.Message);
            Assert.Equal("Access control", read.Value!.Content.Title);
        }
    }

    [Theory]
    [InlineData(false, "attest")]
    [InlineData(true, "advisory")]
    public async Task ShouldAllowManagementWriteGivenNoActualAttestHistoryForThisClient(bool sameTenant,
        string practice)
    {
        // Arrange
        await using var provider = Compose("client_personnel");
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameTenant ? Tenant : Uuid.CreateVersion4(),
            User, true, practice);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        await SeedProgramAsync(provider);
        var control = await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.True(control.IsSuccess, control.Error?.Message);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task ShouldHideHistoricalAttestFactsGivenNoCurrentOwningMembership(bool member,
        bool suspended, bool deprovisioned)
    {
        // Arrange
        await using var provider = Compose("client_personnel", member: member, suspended: suspended,
            deprovisioned: deprovisioned);
        await SeedProgramAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldStillDenyMutationGivenNoOrdinaryProgramGrantAndNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose("client_personnel", permitted: false);
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHideClientMutationGivenMembershipResponseFromAnotherTenant()
    {
        // Arrange
        await using var provider = Compose("client_personnel", wrongTenantMembership: true);
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    static Task SeedProgramAsync(ServiceProvider provider) => ProgramManagementServices.SeedAsync(provider,
        new ProgramRules(Tenant, Program), program =>
        {
            Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client author", Now));
            return Result.Success;
        });

    static ControlDraftContent Content => new("Access control", "Protect systems", "Review access",
        "Owners review access", ["Access review record"]);

    static ServiceProvider Compose(string affiliation, bool member = true, bool suspended = false,
        bool deprovisioned = false, bool permitted = true, bool wrongTenantMembership = false)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(affiliation, member, suspended, deprovisioned, wrongTenantMembership));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(permitted)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships(string affiliation, bool member, bool suspended, bool deprovisioned,
        bool wrongTenantMembership) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(member && tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(userId, wrongTenantMembership ? Uuid.CreateVersion4() : Tenant,
                    affiliation, IsSuspended: suspended,
                    IsDeprovisioned: deprovisioned) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member && tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
