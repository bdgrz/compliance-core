using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class ClassificationPreparationReplayTests
{
    [Fact]
    public async Task ShouldReplayEqualWinnerGivenWorkforcePreparationFailsAfterConcurrentAcceptance()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var request = fixture.Classify(populationId, "ada", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sources = new ConcurrentWinnerSources(fixture.Sources,
            () => fixture.SendAsync(fixture.ManagerUserId, request));
        var handler = new ClassifyAccessPrincipalHandler(services.GetRequiredService<NativeOperationExecutor>(),
            services.GetRequiredService<IAggregateReader>(), sources, services.GetRequiredService<IImmutableContentStore>(),
            fixture.Clock, services.GetRequiredService<RestrictedApplicationVisibility>());
        var context = new InventoryRequestContext<ClassifyAccessPrincipal>(request,
            ProgramManagementServices.Actor(fixture.ManagerUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessPrincipalClassification(fixture.TenantId, populationId, "ada", request.CommandId));

        // Assert
        Assert.Equal(1, sources.Reads);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(sources.Winner, result.Value);
        Assert.Equal(1, owner.Revision);
    }

    sealed class ConcurrentWinnerSources(IAccessReviewSources inner,
        Func<Task<AccessPrincipalClassificationView>> commitWinner) : IAccessReviewSources
    {
        public int Reads { get; private set; }
        public AccessPrincipalClassificationView? Winner { get; private set; }

        public async ValueTask<Result<IReadOnlyList<PersonView>>> ListPeopleAsync(Uuid tenantId,
            CancellationToken ct = default)
        {
            Reads++;
            Winner = await commitWinner();
            return Result<IReadOnlyList<PersonView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The workforce read became unavailable after an equal request committed.", true));
        }

        public ValueTask<Result<IReadOnlyList<string>>> RelationshipStatusesAsync(Uuid tenantId,
            Uuid personId, CancellationToken ct = default) => inner.RelationshipStatusesAsync(tenantId, personId, ct);

        public ValueTask<Result<ServiceIdentityView>> GetServiceIdentityAsync(Uuid tenantId,
            Uuid serviceIdentityId, CancellationToken ct = default) => inner.GetServiceIdentityAsync(tenantId, serviceIdentityId, ct);

        public ValueTask<Result<Uuid?>> AccessOwnerMemberAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => inner.AccessOwnerMemberAsync(tenantId, applicationId, ct);
    }
}
