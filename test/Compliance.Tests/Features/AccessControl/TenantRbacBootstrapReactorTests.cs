using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantRbacBootstrapReactorTests
{
    [Fact]
    public async Task ShouldBootstrapTheCompleteFixedCatalogGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        var grants = TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>().ToArray();
        Assert.Equal(36, grants.Length);
        Assert.Equal(36, grants.Select(grant => (grant.RoleId, grant.Permission)).Distinct().Count());
        Assert.Equal(4, TypedReactionBus.Sent(scenario).OfType<DefineRole>().Count());
        Assert.Equal(4, TypedReactionBus.Sent(scenario).OfType<DefineTeam>().Count());
        Assert.Equal(4, TypedReactionBus.Sent(scenario).OfType<AssignTeamRole>().Count());
        Assert.DoesNotContain(TypedReactionBus.Sent(scenario), request => request is RenameRole);
        Assert.All(grants, grant =>
        {
            Assert.Equal(tenantId, grant.TenantId);
            Assert.NotEqual(Uuid.Empty, grant.CommandId);
            Assert.Equal(0UL, grant.ExpectedRevision);
        });
        Assert.Equal(36, grants.Select(grant => grant.CommandId).Distinct().Count());
        AssertPermissions(grants, BuiltInRbac.TenantAdministrationRoleId(tenantId),
            RbacPermissions.TenantAccess, RbacPermissions.TenantRbacManage, RbacPermissions.ProgramManage,
            RbacPermissions.ApplicationInventoryManage, RbacPermissions.ApplicationRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedRead, RbacPermissions.ComplianceRecordRestrictedWrite,
            RbacPermissions.TechnologyInventoryManage, RbacPermissions.TechnologyInventoryRestrictedRead,
            RbacPermissions.ProviderInventoryManage, RbacPermissions.WorkforceManage, RbacPermissions.AccessReviewManage,
            FieldClasses.WorkforcePersonalDetails.ReadPermission, FieldClasses.WorkforceManagerChain.ReadPermission,
            RbacPermissions.ApplicationImportStage, RbacPermissions.EvidenceRedactionPrepare);
        AssertPermissions(grants, BuiltInRbac.ComplianceManagementRoleId(tenantId),
            RbacPermissions.TenantAccess, RbacPermissions.ProgramManage,
            RbacPermissions.ApplicationInventoryManage, RbacPermissions.ApplicationRestrictedRead,
            RbacPermissions.ComplianceRecordRestrictedRead, RbacPermissions.ComplianceRecordRestrictedWrite,
            RbacPermissions.TechnologyInventoryManage, RbacPermissions.TechnologyInventoryRestrictedRead,
            RbacPermissions.ProviderInventoryManage, RbacPermissions.WorkforceManage, RbacPermissions.AccessReviewManage,
            FieldClasses.WorkforcePersonalDetails.ReadPermission, RbacPermissions.ApplicationImportStage,
            RbacPermissions.EvidenceRedactionPrepare, RbacPermissions.EvidenceRedactionApprove);
        AssertPermissions(grants, BuiltInRbac.ComplianceParticipationRoleId(tenantId),
            RbacPermissions.TenantAccess, RbacPermissions.ApplicationImportStage, RbacPermissions.EvidenceRedactionPrepare);
        AssertPermissions(grants, BuiltInRbac.ViewerRoleId(tenantId),
            RbacPermissions.TenantAccess, RbacPermissions.EvidenceArtifactRead);
    }

    [Fact]
    public async Task ShouldReuseRegistrationCommandIdGivenReplayedTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme", "Acme LLC",
                CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        var registrations = TypedReactionBus.Sent(scenario).OfType<RegisterMember>().ToArray();
        Assert.Equal(2, registrations.Length);
        Assert.Equal(registrations[0], registrations[1]);
        Assert.NotEqual(Uuid.Empty, registrations[0].CommandId);
    }

    [Fact]
    public async Task ShouldReuseAssignmentCommandIdGivenReplayedTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme", "Acme LLC",
                CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        var assignments = TypedReactionBus.Sent(scenario).OfType<AssignTeamMember>().ToArray();
        Assert.Equal(2, assignments.Length);
        Assert.Equal(assignments[0], assignments[1]);
        Assert.NotEqual(Uuid.Empty, assignments[0].CommandId);
    }

    [Fact]
    public async Task ShouldProvisionCreatorAuthorityBeforeActivationGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var adminTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var adminRoleId = BuiltInRbac.TenantAdministrationRoleId(tenantId);
        var memberId = RbacIds.Member(tenantId, creatorId);
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, creatorId, "Acme", "acme",
                "Acme LLC", CreatorIsAdministrator: true, ActivationRequired: true));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        var commands = TypedReactionBus.Sent(scenario).ToList();
        var activationTrigger = commands.FindIndex(request => request is AssignTeamMember assignment &&
            assignment.TeamId == adminTeamId && assignment.MemberId == memberId);
        Assert.True(activationTrigger > 0);
        var prior = commands.Take(activationTrigger).ToArray();
        Assert.Contains(new DefineRole(tenantId, adminRoleId, BuiltInRbac.TenantAdministrationRoleName), prior);
        Assert.Contains(new DefineTeam(tenantId, adminTeamId, BuiltInRbac.AdministratorsTeamName), prior);
        Assert.Contains(prior, request => request is RegisterMember registration &&
            registration.TenantId == tenantId && registration.UserId == creatorId &&
            registration.CommandId != Uuid.Empty);
        Assert.Contains(prior, request => request is AssignTeamRole assignment &&
            assignment.TenantId == tenantId && assignment.TeamId == adminTeamId && assignment.RoleId == adminRoleId &&
            assignment.CommandId != Uuid.Empty && assignment.ExpectedRevision == 0);
        AssertPermissions(prior.OfType<AssignRolePermission>().ToArray(), adminRoleId,
            RbacPermissions.TenantAccess, RbacPermissions.TenantRbacManage, RbacPermissions.ProgramManage);
        Assert.DoesNotContain(prior, request => request is DefineRole role && role.RoleId != adminRoleId);
        Assert.DoesNotContain(commands, request => request is ActivateTenant);
    }

    [Fact]
    public async Task ShouldPreserveInvitationBootstrapGivenAnInvitedFirstAdministrator()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme", "Acme LLC", "admin@example.com"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        Assert.Equal(36, TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>().Count());
        Assert.DoesNotContain(TypedReactionBus.Sent(scenario), request => request is RegisterMember or AssignTeamMember or ActivateTenant);
    }

    [Fact]
    public async Task ShouldRepeatTheSamePermissionOperationsGivenSourceReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));
        var reactor = new TenantRbacBootstrapReactor(new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario));
        await scenario.RunAsync(reactor);
        var first = TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>().ToArray();

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Equal(first, TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>().Skip(36));
    }

    [Theory]
    [InlineData(RequestErrorKind.Conflict)]
    [InlineData(RequestErrorKind.Validation)]
    public async Task ShouldRetainTheSourceCheckpointGivenAnUnacceptedGrant(RequestErrorKind kind)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"))
            .RespondTo<AssignRolePermission>(Result.Failure(new RequestError(kind, "Not accepted.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, TypedReactionBus.For(scenario));

        // Act
        var error = await Assert.ThrowsAsync<ReactionCommandFailedException>(() => scenario.RunAsync(reactor));

        // Assert
        Assert.Equal(kind, error.Error.Kind);
        Assert.Equal(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
        Assert.DoesNotContain(TypedReactionBus.Sent(scenario), request => request is AssignTeamMember);
    }

    [Theory]
    [InlineData(typeof(DefineRole))]
    [InlineData(typeof(DefineTeam))]
    public async Task ShouldContinueGivenABuiltInDefinitionThatConflictsWithAcceptedContent(Type definition)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));
        _ = definition == typeof(DefineRole)
            ? scenario.RespondTo<DefineRole>(Result.Failure(new RequestError(RequestErrorKind.Conflict, "Changed.")))
            : scenario.RespondTo<DefineTeam>(Result.Failure(new RequestError(RequestErrorKind.Conflict, "Changed.")));
        var reactor = new TenantRbacBootstrapReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario));

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Equal(36, TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>().Count());
        Assert.Equal(4, TypedReactionBus.Sent(scenario).OfType<DefineRole>().Count());
        Assert.Equal(4, TypedReactionBus.Sent(scenario).OfType<DefineTeam>().Count());
    }

    [Fact]
    public async Task ShouldRetainTheSourceCheckpointGivenAnInvalidBuiltInDefinition()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"))
            .RespondTo<DefineTeam>(Result.Failure(new RequestError(RequestErrorKind.Validation, "Invalid.")));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, TypedReactionBus.For(scenario));

        // Act
        var error = await Assert.ThrowsAsync<ReactionCommandFailedException>(() => scenario.RunAsync(reactor));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, error.Error.Kind);
        Assert.Equal(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    public static TheoryData<int> GrantCommitBoundaries()
    {
        var positions = new TheoryData<int>();
        foreach (var position in Enumerable.Range(1, 36))
            positions.Add(position);
        return positions;
    }

    [Theory]
    [MemberData(nameof(GrantCommitBoundaries))]
    public async Task ShouldFinishWithoutRestoringRemovedAuthorityGivenAnUnknownGrantAppendOutcome(int interruption)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        await using var provider = BootstrapServices();
        await using var scope = provider.CreateAsyncScope();
        var inner = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var bus = new InterruptedBus(inner, interruption);
        var scenario = new ReactorScenario().Given(new TenantRegistered(tenantId, Uuid.CreateVersion4(),
            "Acme", "acme", "Acme LLC", "admin@example.com"));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, bus);
        await Assert.ThrowsAsync<IOException>(() => scenario.RunAsync(reactor));
        var interrupted = bus.Grants[^1];
        Assert.Equal(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
        var removeInterrupted = interrupted.Permission is not (RbacPermissions.TenantAccess or RbacPermissions.TenantRbacManage);
        if (removeInterrupted)
            Assert.True((await inner.SendAsync(new RemoveRolePermission(tenantId, interrupted.RoleId,
                interrupted.Permission, Uuid.CreateVersion4(), 1, interrupted.CommandId), RequestActor.System)).IsSuccess);

        // Act
        await scenario.RunAsync(reactor);
        await scenario.RunAsync(reactor);

        // Assert
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var grants = bus.Grants.Distinct().ToArray();
        Assert.Equal(36, grants.Length);
        foreach (var grant in grants)
        {
            var owner = await reader.HydrateAsync(new RolePermission(tenantId, grant.RoleId, grant.Permission));
            var removed = removeInterrupted && grant == interrupted;
            Assert.Equal(!removed, owner.IsAssigned);
            Assert.Equal(removed ? 2UL : 1UL, owner.Version);
            Assert.Equal(removed ? null : (Uuid?)grant.CommandId, owner.AssignmentId);
        }
        Assert.All(bus.Identities, identity => Assert.Equal(identity.Typed, identity.Metadata));
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    [Fact]
    public void ShouldRegisterOneCompleteBootstrapGivenApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();

        // Act
        _ = services.AddCompliance(configuration);
        var workloads = services.Where(descriptor => descriptor.ServiceType == typeof(WorkloadRegistration))
            .Select(descriptor => (WorkloadRegistration)descriptor.ImplementationInstance!).ToArray();

        // Assert
        Assert.Equal(WorkloadScope.Global,
            Assert.Single(workloads, workload => workload.ComponentType == typeof(TenantRbacBootstrapReactor)).Scope);
        Assert.DoesNotContain(workloads, workload => workload.Name.Contains("GrantBackfill", StringComparison.Ordinal) ||
            workload.Name is "BuiltInRoleCatalogV1" or "EvidenceRedactionPermissionsV1" or "EvidenceArtifactReadPermissionV1");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ShouldFinishWithoutRestoringRemovedTeamRolesGivenAnUnknownLinkAppendOutcome(int interruption)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        await using var provider = BootstrapServices();
        await using var scope = provider.CreateAsyncScope();
        var inner = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var bus = new InterruptedBus(inner, interruption, interruptRoleLinks: true);
        var scenario = new ReactorScenario().Given(new TenantRegistered(tenantId, Uuid.CreateVersion4(),
            "Acme", "acme", "Acme LLC", "admin@example.com"));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new TenantRbacBootstrapReactor(checkpoints, bus);
        await Assert.ThrowsAsync<IOException>(() => scenario.RunAsync(reactor));
        var interrupted = bus.RoleGrants[^1];
        Assert.Equal(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
        var removed = interrupted.TeamId != BuiltInRbac.AdministratorsTeamId(tenantId);
        if (removed)
            Assert.True((await inner.SendAsync(new RemoveTeamRole(tenantId, interrupted.TeamId, interrupted.RoleId,
                Uuid.CreateVersion4(), 1, interrupted.CommandId), RequestActor.System)).IsSuccess);

        // Act
        await scenario.RunAsync(reactor);
        await scenario.RunAsync(reactor);

        // Assert
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var grants = bus.RoleGrants.Distinct().ToArray();
        Assert.Equal(4, grants.Length);
        foreach (var grant in grants)
        {
            var owner = await reader.HydrateAsync(new TeamRole(tenantId, grant.TeamId, grant.RoleId));
            var isRemoved = removed && grant == interrupted;
            Assert.Equal(!isRemoved, owner.IsAssigned);
            Assert.Equal(isRemoved ? 2UL : 1UL, owner.Version);
            Assert.Equal(isRemoved ? null : (Uuid?)grant.CommandId, owner.AssignmentId);
        }
        Assert.All(bus.Identities, identity => Assert.Equal(identity.Typed, identity.Metadata));
        Assert.Equal(36, bus.Grants.Distinct().Count());
        Assert.NotEqual(ProjectionCheckpoint.Start,
            await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern)));
    }

    static ServiceProvider BootstrapServices() => RbacManagementServices.Build(allowed: true, portia => portia
        .AddRequestHandler<DefineRoleHandler>().AddRequestHandler<DefineTeamHandler>()
        .AddRequestHandler<AssignTeamRoleHandler>().AddRequestHandler<AssignRolePermissionHandler>()
        .AddRequestHandler<RemoveRolePermissionHandler>().AddRequestHandler<RemoveTeamRoleHandler>());

    static void AssertPermissions(IReadOnlyList<AssignRolePermission> grants, Uuid roleId, params string[] expected) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            grants.Where(grant => grant.RoleId == roleId).Select(grant => grant.Permission).Order(StringComparer.Ordinal));

    sealed class InterruptedBus(IRequestBus inner, int interruption, bool interruptRoleLinks = false) : IRequestBus
    {
        public List<AssignRolePermission> Grants { get; } = [];
        public List<AssignTeamRole> RoleGrants { get; } = [];
        public List<(Uuid Typed, Uuid Metadata)> Identities { get; } = [];
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);
        public async ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            var result = await inner.DispatchAsync(request, context, ct);
            if (request is AssignRolePermission grant)
            {
                Grants.Add(grant);
                Identities.Add((grant.CommandId, context.RequestId));
                if (!interruptRoleLinks && Grants.Count == interruption)
                    throw new IOException("Interrupted after the owner committed and before checkpointing.");
            }
            if (request is AssignTeamRole roleGrant)
            {
                RoleGrants.Add(roleGrant);
                Identities.Add((roleGrant.CommandId, context.RequestId));
                if (interruptRoleLinks && RoleGrants.Count == interruption)
                    throw new IOException("Interrupted after the team role committed and before checkpointing.");
            }
            return result;
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchAsync(request, context, ct);
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchStreamAsync(request, context, ct);
    }
}
