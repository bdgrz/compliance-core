using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessPrincipalClassificationCompactionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldPreserveSourceFailureClassificationGivenUnreadableClassificationBody(bool unavailable)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPrincipalClassification(fixture.TenantId, populationId, "ada"));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var content = new FaultedContent(scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            owner.Latest!.DecisionContent.ContentId, unavailable);

        // Act
        var result = await AcceptedAccessPopulation.LoadAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), content, fixture.TenantId, populationId,
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
    }

    sealed class FaultedContent(IImmutableContentStore inner, Uuid selected, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.ContentId != selected
            ? inner.ReadVerifiedAsync(realm, reference, ct)
            : throw (unavailable
                ? new ImmutableContentUnavailableException("Selected source has not become visible.")
                : new ImmutableContentIntegrityException("Selected source is corrupt."));
    }

    [Fact]
    public async Task ShouldKeepClassificationAuthorityBoundedGivenManyNativeDecisions()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        for (var revision = 0; revision < 32; revision++)
            await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human,
                fixture.AdaPersonId, revision: revision);

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPrincipalClassification(fixture.TenantId, populationId, "ada"));
        var principals = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));
        var principal = Assert.Single(principals.Items, value => value.ProviderSubjectId == "ada");

        // Assert
        Assert.Equal(32, owner.Revision);
        Assert.Equal(32, principal.History.Count);
        Assert.Equal(Enumerable.Range(1, 32).Select(static revision => (long)revision),
            principal.History.Select(static decision => decision.Sequence));
        Assert.All(typeof(AccessPrincipalClassification).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(owner) is ICollection { Count: > 2 },
                $"The classification command owner retains lifetime history in {field.Name}."));
    }
}
