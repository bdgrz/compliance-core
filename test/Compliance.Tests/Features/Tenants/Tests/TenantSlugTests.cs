using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantSlugTests
{
    static readonly Uuid FirstTenant = Uuid.Parse(
        "ee6c0952-f147-4aa4-8b65-f57619e3843d",
        CultureInfo.InvariantCulture);
    static readonly Uuid SecondTenant = Uuid.Parse(
        "cd593d48-e9e5-447d-a1a7-502e94a9b7bd",
        CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldDeriveIdentityGivenNormalizedSlug() =>
        Assert.Equal(new TenantSlug("acme").Id, new TenantSlug("ACME").Id);

    [Fact]
    public void ShouldRejectCompetingTenantGivenConcurrentClaim()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(SecondTenant);

        // Assert
        Assert.True(result.IsSuccess);
        var rejectedEvent = scenario.PendingEvents[^1];
        Assert.Equal("TenantSlugRegistrationRejected", rejectedEvent.GetType().Name);
        Assert.Equal(SecondTenant, rejectedEvent.GetType().GetProperty("TenantId")?.GetValue(rejectedEvent));
    }

    [Fact]
    public void ShouldAllowSurrenderGivenOwningTenant()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        var rejected = slug.Surrender(SecondTenant);

        // Act
        var surrendered = slug.Surrender(FirstTenant);

        // Assert
        Assert.True(rejected.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRejected>(scenario.PendingEvents[^2]);
        Assert.True(surrendered.IsSuccess);
        Assert.IsType<TenantSlugSurrendered>(scenario.PendingEvents[^1]);
    }

    // Regression coverage for the backlog's "a retired slug is never assigned to another
    // organization" rule: surrendering must permanently spend a slug, not free it for reuse.
    [Fact]
    public void ShouldStayReservedGivenSurrenderedSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);

        // Act
        _ = slug.Surrender(FirstTenant);

        // Assert
        Assert.False(slug.IsAvailableFor(FirstTenant));
        Assert.False(slug.IsAvailableFor(SecondTenant));
        Assert.Equal(FirstTenant, slug.OwningTenantId);
        Assert.True(slug.IsRetired);
    }

    [Fact]
    public void ShouldRejectRegistrationGivenRetiredSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        _ = slug.Surrender(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(FirstTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<TenantSlugRegistrationRejected>(scenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRejectNewClaimGivenHistoricalSlugLookup()
    {
        // Arrange
        var slug = new TenantSlug("login", allowReserved: true);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(FirstTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(slug.OwningTenantId);
        Assert.IsType<TenantSlugRegistrationRejected>(scenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldDeriveDistinctIdentityGivenDifferentSlugs() =>
        Assert.NotEqual(new TenantSlug("acme").Id, new TenantSlug("acme-next").Id);

    [Fact]
    public void ShouldRefuseConstructionGivenReservedRouteWithoutLookup() =>
        Assert.Throws<ArgumentException>(() => new TenantSlug("login"));

    [Fact]
    public void ShouldReportAlreadyClaimedGivenHoldingTenantPreparesClaim()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        var before = PendingCount(slug);

        // Act
        var prepared = slug.PrepareRegister(FirstTenant);

        // Assert
        Assert.Same(TenantSlug.AlreadyClaimed, prepared.Error);
        Assert.Equal(before, PendingCount(slug));
    }

    [Fact]
    public void ShouldAppendNothingGivenEmptyTenantOnClaimOrSurrender()
    {
        // Arrange
        var slug = new TenantSlug("acme");

        // Act
        var claim = slug.Register(Uuid.Empty);
        var surrender = slug.Surrender(Uuid.Empty);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, claim.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, surrender.Error?.Kind);
        Assert.Equal(0, PendingCount(slug));
    }

    [Fact]
    public void ShouldAppendNothingGivenOperationOwnedByAnotherSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        var foreign = TenantFacts.Provenance(new TenantSlug("other-co"), "RegisterTenantSlug");

        // Act
        var result = slug.Register(foreign, TenantFacts.Response(), FirstTenant);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, PendingCount(slug));
        Assert.Null(slug.OwningTenantId);
    }

    [Fact]
    public void ShouldAppendNothingGivenSurrenderRetriedByAlreadySurrenderedTenant()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        _ = slug.Surrender(FirstTenant);
        var before = PendingCount(slug);

        // Act
        var again = slug.Surrender(FirstTenant);

        // Assert
        Assert.Same(TenantSlug.AlreadySurrendered, again.Error);
        Assert.Equal(before, PendingCount(slug));
    }

    [Fact]
    public void ShouldAnswerRefusalWithoutOwnerGivenSurrenderOfUnclaimedSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Surrender(FirstTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRejected>(scenario.PendingEvents[^1]);
        Assert.Null(slug.OwningTenantId);
        Assert.False(slug.IsRetired);
        Assert.True(slug.IsAvailableFor(SecondTenant));
    }

    [Fact]
    public void ShouldKeepPriorOwnerGivenOtherTenantSurrendersRetiredSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        _ = slug.Surrender(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Surrender(SecondTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRejected>(scenario.PendingEvents[^1]);
        Assert.Equal(FirstTenant, slug.OwningTenantId);
        Assert.True(slug.IsRetired);
    }

    [Fact]
    public void ShouldRestoreRetiredOwnerAndRefuseClaimsGivenValidHistoryReplay()
    {
        // Arrange
        var probe = new TenantSlug("acme");
        var claimId = Uuid.CreateVersion4();
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new TenantSlugRegistered(FirstTenant, "acme", claimId,
                TenantFacts.Provenance(probe, "RegisterTenantSlug", claimId), TenantFacts.Response()), probe.Id, 1),
            DomainEventSeed.Attach(Surrendered(probe, FirstTenant, Uuid.CreateVersion4()), probe.Id, 2),
        };
        var scenario = new AggregateScenario<TenantSlug>(new TenantSlug("acme")).Given(facts);
        var slug = scenario.Aggregate;

        // Act
        var claim = slug.Register(SecondTenant);

        // Assert
        Assert.Equal(FirstTenant, slug.OwningTenantId);
        Assert.True(slug.IsRetired);
        Assert.True(claim.IsSuccess);
        Assert.IsType<TenantSlugRegistrationRejected>(scenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldReplayCommittedClaimGivenExactRetryAfterLaterFacts()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var probe = new TenantSlug("acme");
        var claimOperation = TenantFacts.Provenance(probe, "RegisterTenantSlug", logicalId);
        var facts = new DomainEvent[]
        {
            DomainEventSeed.Attach(new TenantSlugRegistered(FirstTenant, "acme", logicalId, claimOperation,
                TenantFacts.Response()), probe.Id, 1),
            DomainEventSeed.Attach(Surrendered(probe, FirstTenant, Uuid.CreateVersion4()), probe.Id, 2),
        };
        var slug = new AggregateScenario<TenantSlug>(new TenantSlug("acme", logicalId)).Given(facts).Aggregate;

        // Act
        var decision = slug.Acceptance.Decide(AcceptedOperationBinding.Of(claimOperation));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.True(slug.IsRetired);
    }

    [Fact]
    public void ShouldConflictGivenSameLogicalClaimWithChangedIntent()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var probe = new TenantSlug("acme");
        var claimOperation = TenantFacts.Provenance(probe, "RegisterTenantSlug", logicalId);
        var slug = new AggregateScenario<TenantSlug>(new TenantSlug("acme", logicalId))
            .Given(DomainEventSeed.Attach(new TenantSlugRegistered(FirstTenant, "acme", logicalId, claimOperation,
                TenantFacts.Response()), probe.Id, 1)).Aggregate;
        var changed = claimOperation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var decision = slug.Acceptance.Decide(AcceptedOperationBinding.Of(changed));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    static TenantSlugSurrendered Surrendered(TenantSlug probe, Uuid tenantId, Uuid logicalId) =>
        new(tenantId, "acme", logicalId, TenantFacts.Provenance(probe, "SurrenderTenantSlug", logicalId),
            TenantFacts.Response());

    static int PendingCount(TenantSlug slug) => new AggregateScenario<TenantSlug>(slug).PendingEvents.Count;
}
