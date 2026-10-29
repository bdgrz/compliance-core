using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

/// <summary>
///     Runs <see cref="RegisterTenant" /> through Portia's real composed lifecycle — this is what a
///     unit test against <see cref="RegisterTenantAuthorizer" /> or
///     <see cref="RegisterTenantSlugAvailabilityGuard" /> in isolation cannot show: that they are
///     actually wired to <see cref="RegisterTenant" />, and run in the right order.
/// </summary>
public sealed class RegisterTenantRequestScenarioTests
{
    [Fact]
    public async Task ShouldAuthorizeGuardAndHandleGivenAnAvailableSlug()
    {
        // Arrange
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            // Act
            .When(new RegisterTenant("Acme", "acme"))
            // Assert
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutABdgrzIdentity()
    {
        // Arrange
        await using var provider = BuildProvider();

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            // Act
            .When(new RegisterTenant("Acme", "acme"))
            // Assert
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldDenyUserGivenNoVerifiedEmail()
    {
        // Arrange
        await using var provider = BuildProvider(developerAuthentication: false);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            // Act
            .When(new RegisterTenant("Acme", "acme"))
            // Assert
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldCreateTenantGivenVerifiedCreatorWithoutOperatorGrant()
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(developerAuthentication: false,
            verifiedEmailOwner: creatorId);

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(creatorId))
            // Act
            .When(new RegisterTenant("Acme", "acme", "Acme LLC"))
            // Assert
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldRequireLegalNameAndRejectLegacyInvitationGivenProduction()
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(developerAuthentication: false,
            verifiedEmailOwner: creatorId);
        var actor = BdgrzActor(creatorId);

        await RequestScenario.For(provider)
            .GivenActor(actor)
            // Act
            .When(new RegisterTenant("Acme", "acme"))
            // Assert
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectFailure();
        await RequestScenario.For(provider)
            .GivenActor(actor)
            .When(new RegisterTenant("Acme", "acme", "Acme LLC", "creator@example.com"))
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectFailure();
        await RequestScenario.For(provider)
            .GivenActor(actor)
            .When(new RegisterTenant("Acme", "acme", "Acme LLC"))
            .ExpectAuthorized()
            .ExpectGuardPassed<RegisterTenantSlugAvailabilityGuard>()
            .ExpectHandled()
            .ExpectSuccess();
    }

    [Fact]
    public async Task ShouldShortCircuitAtTheGuardGivenAnOccupiedSlug()
    {
        // Arrange
        await using var provider = BuildProvider();
        await using (var seed = provider.CreateAsyncScope())
        {
            var reader = seed.ServiceProvider.GetRequiredService<IAggregateReader>();
            var writer = seed.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var occupied = await reader.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
            _ = occupied.Register(Uuid.CreateVersion4());
            await writer.SaveAsync(occupied, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        await RequestScenario.For(provider)
            .GivenActor(BdgrzActor())
            // Act
            .When(new RegisterTenant("Acme", "acme"))
            // Assert
            .ExpectAuthorized()
            .ExpectGuardFailed<RegisterTenantSlugAvailabilityGuard>(RequestErrorKind.Conflict)
            .ExpectNotHandled();
    }

    static ServiceProvider BuildProvider(bool developerAuthentication = true,
        Uuid? verifiedEmailOwner = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(new PlatformOperatorAuthority([], developerAuthentication));
        services.AddSingleton<IEmailAddressDirectoryReader>(new EmailDirectory(verifiedEmailOwner));
        services.AddPortia()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestAuthorizer<RegisterTenantAuthorizer>()
            .AddRequestGuard<RegisterTenantSlugAvailabilityGuard>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal BdgrzActor(Uuid? userId = null) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", (userId ?? Uuid.CreateVersion4()).ToString())],
        "BdgrzSession"));

    sealed class EmailDirectory(Uuid? verifiedOwner) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress,
            CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(
            verifiedOwner is { } owner && emailAddress == "creator@example.com"
                ? new EmailAddressView(owner, emailAddress, true)
                : null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<EmailAddressView>(
                verifiedOwner is { } owner
                    ? [new EmailAddressView(owner, "creator@example.com", true)] : [], null));
    }
}
