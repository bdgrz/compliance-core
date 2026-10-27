using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessExpectationHistoryTests
{
    [Fact]
    public async Task ShouldKeepSelectedHistoricalAuthorityAndCompleteReadHistoryGivenApprovedSuccessor()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var parameters = new AccessExpectationParameters(PrincipalKind: "access_token");
        var first = await fixture.ApprovedExpectationAsync(AccessReviewVocabulary.ForbiddenPrincipalKind, parameters);
        var proposal = await fixture.SendAsync(fixture.ManagerUserId, fixture.Propose(
            AccessReviewVocabulary.ForbiddenPrincipalKind, parameters, revision: first.Revision,
            supersedes: first.ExpectationId, effectiveFrom: AccessReviewFixture.Observed));
        var successor = await fixture.SendAsync(fixture.ApproverUserId,
            fixture.Approve(proposal.ExpectationId, proposal.Revision));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>().HydrateAsync(new AccessExpectation(
            fixture.TenantId, first.ExpectationId, Uuid.Empty, first.ExpectationId));

        // Act
        var history = await AccessExpectationHistory.ReadAsync(services.GetRequiredService<IDomainEventReader>(), owner);
        var line = await AccessExpectationLine.ReadAsync(owner, services.GetRequiredService<IDomainEventReader>(),
            services.GetRequiredService<IImmutableContentStore>(), fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal(first.ExpectationId, owner.SelectedProposal!.Operation.LogicalId);
        Assert.True(owner.Contains(first.ExpectationId));
        Assert.True(owner.IsApproved(first.ExpectationId));
        Assert.False(owner.Contains(successor.ExpectationId));
        Assert.Equal(4, history.Count);
        Assert.Equal(2, line.Expectations.Count);
        Assert.Equal(AccessExpectation.Approved, line.Find(first.ExpectationId)!.Status);
        Assert.Equal(AccessExpectation.Approved, line.Find(successor.ExpectationId)!.Status);
        Assert.All(line.Expectations, view => Assert.Equal(4, view.Revision));
    }
}
