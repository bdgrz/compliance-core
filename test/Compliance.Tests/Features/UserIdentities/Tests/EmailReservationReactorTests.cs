using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Reactors;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class EmailReservationReactorTests
{
    [Fact]
    public async Task ShouldReserveEmailGivenUserRegistrationWithEmail()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new UserIdentityRegistered(owner, "oidc", "subject", "person@example.com"));

        // Act
        await scenario.RunAsync(Reactor(scenario));

        // Assert
        var request = Assert.IsType<ReserveEmail>(Assert.Single(scenario.SentRequests));
        Assert.Equal(owner, request.UserId);
        Assert.Equal("person@example.com", request.EmailAddress);
    }

    [Fact]
    public async Task ShouldSkipReservationGivenRegistrationWithoutEmail()
    {
        // Arrange
        var scenario = new ReactorScenario().Given(
            new UserIdentityRegistered(Uuid.CreateVersion4(), "oidc", "subject", null));

        // Act
        await scenario.RunAsync(Reactor(scenario));

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldAllowLaterRegistrationsGivenOwnershipConflict()
    {
        // Arrange
        var scenario = new ReactorScenario()
            .Given(new UserIdentityRegistered(Uuid.CreateVersion4(), "oidc", "subject",
                "person@example.com"))
            .RespondTo<ReserveEmail>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The email address belongs to another user.")));

        // Act
        await scenario.RunAsync(Reactor(scenario));

        // Assert
        Assert.IsType<ReserveEmail>(Assert.Single(scenario.SentRequests));
    }

    [Fact]
    public async Task ShouldRemainRetryableGivenDispatchFailure()
    {
        // Arrange
        var scenario = new ReactorScenario()
            .Given(new UserIdentityRegistered(Uuid.CreateVersion4(), "oidc", "subject",
                "person@example.com"))
            .RespondTo<ReserveEmail>(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Unexpected failure.")));

        // Act
        var run = scenario.RunAsync(Reactor(scenario));

        // Assert
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await run);
    }

    static EmailReservationReactor Reactor(ReactorScenario scenario) =>
        new(new InMemoryProjectionCheckpointStore(), scenario.Requests);
}
