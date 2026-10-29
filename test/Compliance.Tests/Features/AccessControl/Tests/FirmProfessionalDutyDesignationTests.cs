using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>
///     Direct admission decisions of one professional-duty designation owner: identity, one pending change at a
///     time, stale revision and refusals that append nothing. Applied-change history is covered by the process tests.
/// </summary>
public sealed class FirmProfessionalDutyDesignationTests
{
    static readonly ImmutableContentRef Content = new(Uuid.Parse("00000000-0000-4000-8000-0000000000a1",
        System.Globalization.CultureInfo.InvariantCulture), new string('a', 64), 2, "application/json", "test.v1");

    [Fact]
    public void ShouldKeepIdentityStableAndDistinctGivenSubjectDutyAndScope()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();

        // Act
        var platform = FirmProfessionalDutyDesignation.IdentityOf(user, FirmProfessionalDuty.RuleRatifier, null);
        var platformAgain = FirmProfessionalDutyDesignation.IdentityOf(user, FirmProfessionalDuty.RuleRatifier, null);
        var otherDuty = FirmProfessionalDutyDesignation.IdentityOf(user, FirmProfessionalDuty.EngagementPartner, null);
        var tenantScoped = FirmProfessionalDutyDesignation.IdentityOf(user, FirmProfessionalDuty.RuleRatifier, tenant);
        var emptyTenant = FirmProfessionalDutyDesignation.IdentityOf(user, FirmProfessionalDuty.RuleRatifier, Uuid.Empty);
        var otherUser = FirmProfessionalDutyDesignation.IdentityOf(Uuid.CreateVersion4(), FirmProfessionalDuty.RuleRatifier, null);

        // Assert
        Assert.Equal(platform, platformAgain);
        Assert.NotEqual(platform, otherDuty);
        Assert.NotEqual(platform, tenantScoped);
        Assert.NotEqual(platform, emptyTenant);
        Assert.NotEqual(platform, otherUser);
        Assert.Equal(FirmProfessionalDutyDesignation.Area, new FirmProfessionalDutyDesignation(user, FirmProfessionalDuty.RuleRatifier, null).Stream.Area);
    }

    [Fact]
    public void ShouldAcceptOnePendingChangeAtRevisionZeroGivenFreshDesignation()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var assertion = Uuid.CreateVersion4();

        // Act
        var result = owner.AcceptChange(Provenance(owner, assertion, 0), Content, Content);

        // Assert
        Assert.True(result.IsSuccess);
        var accepted = Assert.IsType<ProfessionalDutyChangeAccepted>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(0L, accepted.Revision);
        Assert.Equal(owner.Stream, accepted.Owner);
        Assert.Equal(assertion, accepted.Operation.RootOperationId);
        Assert.Equal(accepted, owner.Pending);
        Assert.Equal(0UL, owner.Revision);
        Assert.Null(owner.Latest);
    }

    [Fact]
    public void ShouldRefuseSecondChangeWithoutFactGivenAnotherChangeStillPending()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        Assert.True(owner.AcceptChange(Provenance(owner, Uuid.CreateVersion4(), 0), Content, Content).IsSuccess);

        // Act
        var result = owner.AcceptChange(Provenance(owner, Uuid.CreateVersion4(), 0), Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSameAssertionWithoutFactGivenChangeAlreadyAccepted()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var assertion = Uuid.CreateVersion4();
        Assert.True(owner.AcceptChange(Provenance(owner, assertion, 0), Content, Content).IsSuccess);

        // Act
        var result = owner.AcceptChange(Provenance(owner, assertion, 0), Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.False(result.Error.IsTransient);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangeWithoutFactGivenStaleExpectedRevision()
    {
        // Arrange
        var owner = Fresh(out var scenario);

        // Act
        var result = owner.AcceptChange(Provenance(owner, Uuid.CreateVersion4(), 5), Content, Content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public void ShouldRefuseChangeWithoutFactGivenOperationOfAnotherOwnerOrAnotherLogicalId()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var other = new FirmProfessionalDutyDesignation(Uuid.CreateVersion4(), FirmProfessionalDuty.RuleRatifier, null);
        var assertion = Uuid.CreateVersion4();
        var foreign = Provenance(owner, assertion, 0) with { Owner = other.Stream };
        var nested = Provenance(owner, assertion, 0) with { LogicalId = Uuid.CreateVersion4() };

        // Act
        var foreignResult = owner.AcceptChange(foreign, Content, Content);
        var nestedResult = owner.AcceptChange(nested, Content, Content);

        // Assert
        Assert.False(foreignResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, foreignResult.Error!.Kind);
        Assert.False(nestedResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, nestedResult.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseApplyAndRejectWithoutFactGivenNoPendingChange()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        var operation = Provenance(owner, Uuid.CreateVersion4(), 0);

        // Act
        var applied = owner.Apply(ProfessionalDutyChangeKind.Record, operation, Content, "signed", Content);
        var rejected = owner.Reject(operation, Content);

        // Assert
        Assert.False(applied.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, applied.Error!.Kind);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseApplyWithoutFactGivenOtherPendingChange()
    {
        // Arrange
        var owner = Fresh(out var scenario);
        Assert.True(owner.AcceptChange(Provenance(owner, Uuid.CreateVersion4(), 0), Content, Content).IsSuccess);
        var stranger = Provenance(owner, Uuid.CreateVersion4(), 0);

        // Act
        var applied = owner.Apply(ProfessionalDutyChangeKind.Revoke, stranger, Content, "signed", Content);
        var rejected = owner.Reject(stranger, Content);

        // Assert
        Assert.False(applied.IsSuccess);
        Assert.False(rejected.IsSuccess);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseEngagementUseWithoutFactGivenNoRecordedDesignation()
    {
        // Arrange
        var owner = Fresh(out var scenario);

        // Act
        var result = owner.AuthorizeEngagementUse(Provenance(owner, Uuid.CreateVersion4(), 0), Content, 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    static FirmProfessionalDutyDesignation Fresh(out AggregateScenario<FirmProfessionalDutyDesignation> scenario)
    {
        var owner = new FirmProfessionalDutyDesignation(Uuid.CreateVersion4(), FirmProfessionalDuty.RuleRatifier, null);
        scenario = new AggregateScenario<FirmProfessionalDutyDesignation>(owner);
        return owner;
    }

    static OperationProvenance Provenance(FirmProfessionalDutyDesignation owner, Uuid assertion, ulong expected) =>
        new(assertion, assertion, null, "ChangeProfessionalDuty",
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator"), owner.Stream, "initial", expected,
            new string('d', 64), DateTimeOffset.UtcNow, owner.Stream, owner.Stream);
}
