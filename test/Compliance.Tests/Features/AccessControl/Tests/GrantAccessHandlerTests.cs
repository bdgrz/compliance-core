using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class GrantAccessHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();
    static readonly Uuid PrincipalMemberId = Uuid.CreateVersion4();
    static readonly Uuid RoleId = Uuid.CreateVersion4();
    static readonly Uuid GrantId = Uuid.CreateVersion4();
    static readonly DateTimeOffset EffectiveFrom = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldBindMemberGrantToCurrentMembershipEpisodeGivenValidProposal()
    {
        // Arrange
        await using var provider = CreateProvider();
        var episodeId = Uuid.CreateVersion4();
        var handler = new GrantAccessHandler(provider.GetRequiredService<IAggregateExecutor>(),
            new ValidProposalValidator(), new TestMemberAccessEligibility(episodeId, episodeId), provider.GetRequiredService<IAggregateReader>());
        var request = Request();

        // Act
        var result = await handler.HandleAsync(new RequestContext<GrantAccess>(request, Actor()),
            CancellationToken.None);
        var history = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IEventStore>()
                           .ReadAsync(new AccessGrant(TenantId, GrantId).Stream, 0))
            history.Add(record.Event);

        // Assert
        Assert.True(result.IsSuccess);
        var issued = Assert.IsType<AccessGrantIssued>(Assert.Single(history));
        Assert.Equal(episodeId, issued.MembershipEpisodeId);
    }

    [Fact]
    public async Task ShouldRejectMemberGrantGivenMembershipEpisodeChangesDuringValidation()
    {
        // Arrange
        await using var provider = CreateProvider();
        var firstEpisodeId = Uuid.CreateVersion4();
        var secondEpisodeId = Uuid.CreateVersion4();
        var handler = new GrantAccessHandler(provider.GetRequiredService<IAggregateExecutor>(),
            new ValidProposalValidator(), new TestMemberAccessEligibility(firstEpisodeId, secondEpisodeId), provider.GetRequiredService<IAggregateReader>());

        // Act
        var result = await handler.HandleAsync(new RequestContext<GrantAccess>(Request(), Actor()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        await foreach (var record in provider.GetRequiredService<IEventStore>()
                           .ReadAsync(new AccessGrant(TenantId, GrantId).Stream, 0))
            Assert.IsNotType<AccessGrantIssued>(record.Event);
    }

    static GrantAccess Request() => new(TenantId, GrantId, new AccessGrantProposal(
        new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, PrincipalMemberId), RoleId,
        new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
        new AccessGrantSource("manual", "access-review"), EffectiveFrom, null));

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        return services.BuildServiceProvider();
    }

    sealed class ValidProposalValidator : IAccessGrantProposalValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, AccessGrantProposal proposal,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }

    sealed class TestMemberAccessEligibility(params Uuid?[] episodes) : IMemberAccessEligibility
    {
        int _episodeIndex;

        public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(true);

        public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId,
            CancellationToken ct = default)
        {
            var index = Math.Min(_episodeIndex++, episodes.Length - 1);
            return ValueTask.FromResult(episodes[index]);
        }
    }
}
