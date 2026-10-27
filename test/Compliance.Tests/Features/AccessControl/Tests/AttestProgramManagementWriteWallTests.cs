using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Workforce.Rules;
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
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries.Projections;
using Bdgrz.Compliance.Features.Boundaries.Queries;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AttestProgramManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("commitment")]
    [InlineData("mapping")]
    [InlineData("overlay")]
    [InlineData("evaluation_plan")]
    [InlineData("finding")]
    [InlineData("readiness")]
    [InlineData("training")]
    public async Task ShouldDenyClientManagementRecordGivenActualAttestHistoryAndOrdinaryProgramGrant(string operation)
    {
        // Arrange
        await using var provider = Compose();
        await ProgramManagementServices.SeedAsync(provider, new ProgramRules(Tenant, Program), source =>
        {
            Assert.Null(source.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client administrator", Now));
            return Result.Success;
        });
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(User),
            new HttpInvocation("POST", "/synthetic/program-management", "/synthetic/program-management", "synthetic"));

        // Act
        var error = operation switch
        {
            "program" => (await bus.DispatchAsync(new CreateProgram(Uuid.CreateVersion4(), Tenant, "New program",
                new ProgramPlan(null, null, null, null, null, null)), context, CancellationToken.None)).Error,
            "service" => (await bus.DispatchAsync(new CreateClientService(Uuid.CreateVersion4(), Tenant, Program, "Service",
                "Client service purpose", "Client owner"), context, CancellationToken.None)).Error,
            "boundary" => (await bus.DispatchAsync(new CreateBoundary(Uuid.CreateVersion4(), Tenant, Program,
                new BoundaryContent("Client boundary", "readiness", ["security"], [])), context, CancellationToken.None)).Error,
            "commitment" => (await bus.DispatchAsync(new CreateCommitmentDraft(Bdgrz.Compliance.Features.Commitments.Aggregates.CommitmentDraft.IdFor(Tenant, Program, "commitment", "COM-1"), Tenant, Program,
                Uuid.CreateVersion4(), "commitment", "COM-1", "Commitment statement", "Context", "Source"), context, CancellationToken.None)).Error,
            "mapping" => (await bus.DispatchAsync(new ProposeCriterionNotApplicable(Uuid.CreateVersion4(), Tenant, Program,
                Uuid.CreateVersion4(), "CC1.1", 0, "Client applicability decision"), context, CancellationToken.None)).Error,
            "overlay" => (await bus.DispatchAsync(new SetCriteriaTextOverlay(Uuid.CreateVersion4(), Tenant, Uuid.CreateVersion4(),
                "CC1.1", 0, new CriteriaTextOverlayContent("Licensed text", "Supplier", "License",
                    new CriteriaOverlayUsageFlags(true, false))), context, CancellationToken.None)).Error,
            "evaluation_plan" => (await bus.DispatchAsync(new DefineControlEvaluationPlan(Tenant, Program,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, "Review control design", [], true), context, CancellationToken.None)).Error,
            "finding" => (await bus.DispatchAsync(new RaiseFinding(Uuid.CreateVersion4(), Tenant, Program,
                new FindingSource("manual", null, null, "Client source finding"), "Finding", "Description", "medium",
                "Client scope", RbacIds.Member(Tenant, User), new DateOnly(2027, 1, 1)), context, CancellationToken.None)).Error,
            "readiness" => (await bus.DispatchAsync(new RunReadinessAssessment(Uuid.CreateVersion4(), Tenant, Program, 0,
                Now), context, CancellationToken.None)).Error,
            "training" => (await bus.DispatchAsync(new DefineTrainingRequirement(TrainingRequirementIdentity.IdFor(Tenant, Program, "TR-1"), Tenant, Program, "TR-1",
                new TrainingRequirementContent("Security training", "Training description", "core_security", null,
                    "manual")), context, CancellationToken.None)).Error,
            _ => throw new InvalidOperationException()
        };

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("Attest", error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPreserveOwnPersonalPolicyAcknowledgementGivenActualAttestHistoryAndOnlyOrdinaryReadGrant()
    {
        // Arrange
        await using var provider = Compose(readOnly: true);
        await SeedProgramAsync(provider);
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(Tenant, User);
        var actor = ActorReference.ForMember(memberId, "Client participant");
        await ProgramManagementServices.SeedAsync(provider, new PersonRules(Tenant, personId), person =>
        {
            Assert.True(person.Record("Client participant", null, actor, Now).IsSuccess);
            Assert.Null(person.CorrelateMembership(1, User, actor, Now));
            return Result.Success;
        });
        await CampaignSeed.LaunchAsync(provider, Tenant, Program, campaignId,
            new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-1", "Synthetic frozen policy", 1,
                "synthetic-pinned-policy-hash"), PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(),
            "synthetic-frozen-roster-hash", new RosterAudience(
                new Dictionary<Uuid, RosterMatch>
                {
                    [personId] = new(personId, "Client participant", "employee", "Engineering", new DateOnly(2025, 1, 1)),
                }, new HashSet<Uuid> { personId }), new DateOnly(2027, 1, 1), null, actor, memberId, Now);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new AcknowledgePolicy(Uuid.CreateVersion4(), Tenant, Program, campaignId, personId, 1,
                "synthetic-pinned-policy-hash", PolicyDistributionCampaign.DefaultAcknowledgementText, 1),
            new RequestDispatchContext(ProgramManagementServices.Actor(User),
                new HttpInvocation("POST", "/synthetic/policy-acknowledgement", "/synthetic/policy-acknowledgement", "synthetic")),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var applied = await CampaignSeed.ApplyAdmittedAsync(provider, Tenant, campaignId, personId,
            result.Value!.OperationId, CampaignParticipantResultKind.PolicyAcknowledgement);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        var acknowledgement = (await CampaignSeed.ParticipantAsync(provider, Tenant, campaignId, personId))
            .Acknowledgement!;
        Assert.False(acknowledgement.RecordedOnBehalf);
        Assert.Equal(personId.ToString(), acknowledgement.Performer.Id);
        Assert.Equal(memberId.ToString(), acknowledgement.Recorder.Id);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPermitProgramManagementGivenNoSameClientActualAttestHistory(string practice,
        bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(),
            User, revoked: true, practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateProgram(Uuid.CreateVersion4(), Tenant, "New program", new ProgramPlan(null, null, null, null, null, null)),
            ProgramManagementServices.Actor(User));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldPreserveSharedBoundaryReadGivenActualAttestHistoryAndOrdinaryProgramGrant()
    {
        // Arrange
        await using var provider = Compose();
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(User);
        var created = await bus.SendAsync(new CreateBoundary(Uuid.CreateVersion4(), Tenant, Program,
            new BoundaryContent("Client boundary", "readiness", ["security"], [])), actor);
        Assert.True(created.IsSuccess, created.Error?.Message);
        await ProjectBoundariesAsync(scope.ServiceProvider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var read = await bus.SendAsync(new GetBoundary(Tenant, created.Value!.BoundaryId), actor);

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("Client boundary", read.Value!.Draft!.Content.Statement);
    }

    static Task SeedProgramAsync(IServiceProvider provider) => ProgramManagementServices.SeedAsync(provider,
        new ProgramRules(Tenant, Program), program =>
        {
            Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client administrator", Now));
            return Result.Success;
        });

    static async Task ProjectBoundariesAsync(IServiceProvider services)
    {
        var events = services.GetRequiredService<IDomainEventReader>();
        var projection = services.GetRequiredService<IBoundaryDirectoryProjection>();
        var identity = new CheckpointIdentity(FitzBoundaryDirectory.ProjectorName, EventStreamPattern.ForPattern(Tenant.ToString()));
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var cursor = ProjectionCheckpoint.Start.Cursor;
        await foreach (var record in events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
        {
            await projection.ApplyAsync(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static ServiceProvider Compose(bool readOnly = false)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            readOnly ? new ReadOnlyPermissions() : new RecordingPermissionAuthorizer(true)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class ReadOnlyPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == IProgramReadRequest.ReadPermission);
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(userId, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
