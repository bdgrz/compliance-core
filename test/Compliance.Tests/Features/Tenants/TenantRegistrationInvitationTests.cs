using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantRegistrationInvitationTests
{
    [Fact]
    public async Task ShouldReuseFirstAdministratorInvitationIdGivenReplayedRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme",
            FirstAdministratorEmail: "admin@example.com"));

        // Act
        await scenario.RunAsync(new TenantRegistrationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario)));
        await scenario.RunAsync(new TenantRegistrationReactor(new InMemoryProjectionCheckpointStore(),
            TypedReactionBus.For(scenario)));

        // Assert
        var invitations = TypedReactionBus.Sent(scenario).OfType<InviteTenantMember>().ToArray();
        Assert.Equal(2, invitations.Length);
        Assert.Equal(invitations[0], invitations[1]);
        Assert.Equal(TenantInvitationIds.FirstAdministratorOf(tenantId), invitations[0].InvitationId);
        Assert.True(invitations[0].Administrator);
        Assert.Equal("client_personnel", invitations[0].Affiliation);
    }

    [Fact]
    public void ShouldDeriveDistinctIdsGivenDifferentTenantsAndPurposes()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();

        // Act
        var firstAdministrator = TenantInvitationIds.FirstAdministratorOf(first);

        // Assert
        Assert.Equal(firstAdministrator, TenantInvitationIds.FirstAdministratorOf(first));
        Assert.NotEqual(firstAdministrator, TenantInvitationIds.FirstAdministratorOf(second));
        Assert.NotEqual(firstAdministrator, TenantInvitationIds.AcceptanceOf(first));
    }

    [Fact]
    public async Task ShouldInviteFirstAdministratorAsOperatorAndReplayExactlyGivenSystemReaction()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var owner = Uuid.CreateVersion4();
        await using (var scope = host.Provider.CreateAsyncScope())
        {
            var seeded = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
                new Tenant(host.TenantId), tenant => AggregateOutcome.CommitOnSuccess(
                    tenant.Register(owner, "Acme", "acme").IsSuccess ? Result.Success : Result.Failure(
                        new RequestError(RequestErrorKind.Validation, "The tenant must register."))),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(seeded.IsSuccess);
        }

        var invitationId = TenantInvitationIds.FirstAdministratorOf(host.TenantId);
        var request = new InviteTenantMember(invitationId, host.TenantId, "admin@example.com", "client_personnel", true);

        // Act
        var first = await HandleAsync(host, request);
        var retry = await HandleAsync(host, request);
        var invited = await host.InvitedAsync(invitationId);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Single(await host.InvitationFactsAsync(invitationId));
        Assert.True(invited.Administrator);
        Assert.Equal(owner, invited.InvitedBy);
        Assert.Equal("system_process", invited.Operation.Actor.Kind);
    }

    [Fact]
    public async Task ShouldRejectInvitationGivenTenantIsNotRegistered()
    {
        // Arrange
        await using var host = new TenantInvitationHost();

        // Act
        var result = await HandleAsync(host, new InviteTenantMember(Uuid.CreateVersion4(), host.TenantId,
            "admin@example.com", "client_personnel", true));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Empty(await host.MailboxFactsAsync("admin@example.com"));
    }

    static async Task<Result> HandleAsync(TenantInvitationHost host, InviteTenantMember request)
    {
        await using var scope = host.Provider.CreateAsyncScope();
        var handler = new InviteTenantMemberHandler(scope.ServiceProvider.GetRequiredService<TenantInvitationAdmission>(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>());
        return await handler.HandleAsync(new RequestContext<InviteTenantMember>(request, RequestActor.System),
            CancellationToken.None);
    }
}
