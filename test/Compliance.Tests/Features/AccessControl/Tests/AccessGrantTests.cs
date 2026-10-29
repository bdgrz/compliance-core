using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessGrantTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid GrantId = Uuid.Parse("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3", CultureInfo.InvariantCulture);
    static readonly Uuid MemberId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = Uuid.Parse("5b66f817-415a-44e8-b809-503cf44b1537", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset EffectiveFrom = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldIssueGrantWithScopeAndProvenanceGivenValidTerms()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            EffectiveFrom.AddDays(30));
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId));

        // Act
        var result = scenario.Aggregate.Issue(terms);

        // Assert
        Assert.True(result.IsSuccess);
        var issued = Assert.IsType<AccessGrantIssued>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(TenantId, issued.TenantId);
        Assert.Equal(GrantId, issued.GrantId);
        Assert.Equal(terms, issued.Terms);
    }

    [Fact]
    public void ShouldRejectInvalidEffectiveIntervalGivenGrantTerms()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            EffectiveFrom);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId));

        // Act
        var result = scenario.Aggregate.Issue(terms);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectScopeOwnedByAnotherTenantGivenGrantTerms()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId));

        // Act
        var result = scenario.Aggregate.Issue(terms);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRequireResourceTypeGivenSharedResourceScope()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.SharedResource, Uuid.CreateVersion4()),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId));

        // Act
        var result = scenario.Aggregate.Issue(terms);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectChangedTermsGivenExistingGrantId()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId))
            .Given(DomainEventSeed.Attach(new AccessGrantIssued(TenantId, GrantId, terms), GrantId, 1));

        // Act
        var result = scenario.Aggregate.Issue(terms with
        {
            Source = new AccessGrantSource("manual", "request-456"),
        });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReplayOriginalCreationWithoutRestoringAuthorityGivenPreviouslyRevokedGrantId()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId))
            .Given(
                DomainEventSeed.Attach(new AccessGrantIssued(TenantId, GrantId, terms), GrantId, 1),
                DomainEventSeed.Attach(new AccessGrantRevoked(TenantId, GrantId,
                    ActorReference.ForMember(MemberId, "Organization Admin"),
                    EffectiveFrom.AddDays(1)), GrantId, 2));

        // Act
        var result = scenario.Aggregate.Issue(terms);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(scenario.Aggregate.IsRevoked);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectRepeatedRevocationGivenPreviouslyRevokedGrant()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var scenario = new AggregateScenario<AccessGrant>(new AccessGrant(TenantId, GrantId))
            .Given(
                DomainEventSeed.Attach(new AccessGrantIssued(TenantId, GrantId, terms), GrantId, 1),
                DomainEventSeed.Attach(new AccessGrantRevoked(TenantId, GrantId,
                    ActorReference.ForMember(MemberId, "Organization Admin"),
                    EffectiveFrom.AddDays(1)), GrantId, 2));

        // Act
        var result = scenario.Aggregate.Revoke(ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom.AddDays(2));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRecordRevocationSeparatelyGivenIssuedGrant()
    {
        // Arrange
        var terms = new AccessGrantTerms(
            new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
            RoleId,
            new AccessGrantScope(AccessGrantScopeKind.Organization, TenantId),
            new AccessGrantSource("manual", "request-123"),
            ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom,
            null);
        var grant = new AccessGrant(TenantId, GrantId);
        var scenario = new AggregateScenario<AccessGrant>(grant)
            .Given(DomainEventSeed.Attach(new AccessGrantIssued(TenantId, GrantId, terms), GrantId, 1));

        // Act
        var result = scenario.Aggregate.Revoke(ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Collection(scenario.PendingEvents,
            ev => Assert.IsType<AccessGrantRevoked>(ev));
        var revoked = Assert.IsType<AccessGrantRevoked>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(ActorReference.ForMember(MemberId, "Organization Admin"), revoked.RevokedBy);
        Assert.Equal(EffectiveFrom.AddDays(1), revoked.RevokedAt);
    }

    [Fact]
    public void ShouldRejectRevocationWithoutIssuedGrantGivenUnknownGrant()
    {
        // Arrange
        var grant = new AccessGrant(TenantId, GrantId);
        var scenario = new AggregateScenario<AccessGrant>(grant);

        // Act
        var result = scenario.Aggregate.Revoke(ActorReference.ForMember(MemberId, "Organization Admin"),
            EffectiveFrom);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(scenario.PendingEvents);
    }
}
