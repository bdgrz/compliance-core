using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessExpectationIdentityTests
{
    [Fact]
    public async Task ShouldRefuseAmbiguousApprovalGivenSameSuccessorIdAdmittedOnIndependentLines()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (first, _, firstCommand, _) = await SeedConcurrentIdentityAsync(fixture);
        var command = fixture.Approve(firstCommand.ExpectationId, first.Revision);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ApproveAccessExpectationHandler>(scope.ServiceProvider);
        var context = new Cntryl.Portia.RequestContext<ApproveAccessExpectation>(command,
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.ApproverUserId),
                new HttpInvocation("PUT", "/access-expectations/approval", "/access-expectations/approval", "identity-test")));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess, "A scoped identity with two authoritative first owners must never choose the first one.");
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldReplayEachOriginalProposalGivenSameScopedIdentityOnIndependentFirstOwners()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (first, second, firstCommand, secondCommand) = await SeedConcurrentIdentityAsync(fixture);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(scope.ServiceProvider);

        // Act
        var firstRetry = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(firstCommand,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);
        var secondRetry = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(secondCommand,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);

        // Assert
        Assert.True(firstRetry.IsSuccess, firstRetry.Error?.Message);
        Assert.True(secondRetry.IsSuccess, secondRetry.Error?.Message);
        Assert.Equal(first, firstRetry.Value);
        Assert.Equal(second, secondRetry.Value);
        Assert.NotEqual(first.SupersedesExpectationId, second.SupersedesExpectationId);
    }

    static async Task<(AccessExpectationView First, AccessExpectationView Second,
        ProposeAccessExpectation FirstCommand, ProposeAccessExpectation SecondCommand)> SeedConcurrentIdentityAsync(
        AccessReviewFixture fixture)
    {
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var rootA = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var rootB = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var shared = Uuid.CreateVersion4();
        var first = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: rootA.Revision,
            expectationId: shared, supersedes: rootA.ExpectationId, effectiveFrom: AccessReviewFixture.Observed);
        var second = fixture.Propose(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: rootB.Revision,
            expectationId: shared, supersedes: rootB.ExpectationId, effectiveFrom: AccessReviewFixture.Observed);
        var admittedA = await fixture.SendAsync(fixture.ManagerUserId, first);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // Both first owners can legally append after discovery ran before either candidate was visible. Hide only
        // the other admission from this command's scoped discovery; actual native preparation/append use real sources.
        var discovery = new AccessExpectationsReader(services.GetRequiredService<IAggregateReader>(),
            new BeforeOtherAdmission(services.GetRequiredService<IDomainEventReader>(), shared),
            services.GetRequiredService<Bdgrz.Compliance.Features.Content.IImmutableContentStore>());
        var handler = ActivatorUtilities.CreateInstance<ProposeAccessExpectationHandler>(services, discovery);
        var admittedB = await handler.HandleAsync(new InventoryRequestContext<ProposeAccessExpectation>(second,
            ProgramManagementServices.Actor(fixture.ManagerUserId)), CancellationToken.None);
        Assert.True(admittedB.IsSuccess, admittedB.Error?.Message);
        return (admittedA, admittedB.Value, first, second);
    }

    sealed class BeforeOtherAdmission(IDomainEventReader inner, Uuid shared) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => inner.ReadAsync(address, fromOffset, ct);
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct).ConfigureAwait(false))
                if (record.Event is not AccessExpectationProposed proposed || proposed.Operation.LogicalId != shared)
                    yield return record;
        }
    }
}
