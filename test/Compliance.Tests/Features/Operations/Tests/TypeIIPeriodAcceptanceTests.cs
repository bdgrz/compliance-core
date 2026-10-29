using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

/// <summary>Direct tests of the compact TypeIIPeriod acceptance, fence and refusal decisions.</summary>
public sealed class TypeIIPeriodAcceptanceTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAppendNothingGivenOpeningWithoutFrozenFirstResponse()
    {
        // Arrange
        var period = NewPeriod();
        var notFrozen = Content("synthetic-content.v1");

        // Act
        var result = period.AcceptOpening(Provenance(period, period.Id), Content("intent.v1"), notFrozen);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<TypeIIPeriod>(period).PendingEvents);
        Assert.Null(period.Pending);
    }

    [Fact]
    public void ShouldAppendOneOpeningFactGivenValidEnvelopeAndRefuseSecondOpening()
    {
        // Arrange
        var period = NewPeriod();
        var intent = Content("intent.v1");

        // Act
        var first = period.AcceptOpening(Provenance(period, period.Id), intent, Content(FirstResponses.Schema));
        var second = period.AcceptOpening(Provenance(period, period.Id), intent, Content(FirstResponses.Schema));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.False(second.IsSuccess);
        Assert.Single(new AggregateScenario<TypeIIPeriod>(period).PendingEvents);
        Assert.IsType<TypeIIPeriodOpeningAccepted>(period.Pending);
        Assert.Equal(period.Id, period.PendingOperationId);
        Assert.Equal(ProgramId, period.ProgramId);
    }

    [Fact]
    public void ShouldAppendNothingGivenOpeningAffectingAnotherStream()
    {
        // Arrange
        var period = NewPeriod();
        var other = NewPeriod();
        var foreign = Provenance(period, period.Id) with { AffectedOwner = other.Stream };

        // Act
        var result = period.AcceptOpening(foreign, Content("intent.v1"), Content(FirstResponses.Schema));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<TypeIIPeriod>(period).PendingEvents);
        Assert.Null(period.Pending);
    }

    [Fact]
    public void ShouldAppendNothingGivenOpeningRootThatIsNotThePeriod()
    {
        // Arrange
        var period = NewPeriod();

        // Act
        var result = period.AcceptOpening(Provenance(period, Uuid.CreateVersion4()), Content("intent.v1"),
            Content(FirstResponses.Schema));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<TypeIIPeriod>(period).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseAmendmentAndCancelGivenOpeningNotYetApplied()
    {
        // Arrange
        var period = NewPeriod();
        Assert.True(period.AcceptOpening(Provenance(period, period.Id), Content("intent.v1"),
            Content(FirstResponses.Schema)).IsSuccess);
        var raised = new AggregateScenario<TypeIIPeriod>(period).PendingEvents.Count;

        // Act
        var result = period.AcceptAmendment(0, Provenance(period, Uuid.CreateVersion4()), Content("intent.v1"),
            Content(FirstResponses.Schema));
        var cancel = period.PrepareCancel(period.Id, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(cancel.IsSuccess);
        Assert.Equal(raised, new AggregateScenario<TypeIIPeriod>(period).PendingEvents.Count);
        Assert.Equal(period.Id, period.PendingOperationId);
    }

    [Fact]
    public void ShouldFenceOpeningForeverGivenCalendarRejection()
    {
        // Arrange
        var period = NewPeriod();
        var intent = Content("intent.v1");
        Assert.True(period.AcceptOpening(Provenance(period, period.Id), intent, Content(FirstResponses.Schema)).IsSuccess);

        // Act
        var rejected = period.RejectOpening(Provenance(period, period.Id), Content("failure-proof.v1"));
        var again = period.RejectOpening(Provenance(period, period.Id), Content("failure-proof.v1"));
        var reopened = period.AcceptOpening(Provenance(period, period.Id), intent, Content(FirstResponses.Schema));

        // Assert
        Assert.True(rejected.IsSuccess, rejected.Error?.Message);
        Assert.False(again.IsSuccess);
        Assert.False(reopened.IsSuccess);
        Assert.Equal(2, new AggregateScenario<TypeIIPeriod>(period).PendingEvents.Count);
        Assert.Null(period.Pending);
        Assert.Equal(0, period.Revision);
    }

    [Fact]
    public void ShouldKeepOpeningPendingGivenRejectionNamingAnotherOperation()
    {
        // Arrange
        var period = NewPeriod();
        Assert.True(period.AcceptOpening(Provenance(period, period.Id), Content("intent.v1"),
            Content(FirstResponses.Schema)).IsSuccess);

        // Act
        var result = period.RejectOpening(Provenance(period, Uuid.CreateVersion4()), Content("failure-proof.v1"));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.IsType<TypeIIPeriodOpeningAccepted>(period.Pending);
        Assert.Single(new AggregateScenario<TypeIIPeriod>(period).PendingEvents);
    }

    static TypeIIPeriod NewPeriod() => new(TenantId, ProgramId, Uuid.CreateVersion4());

    static ImmutableContentRef Content(string schema) => new(Uuid.CreateVersion4(), new string('a', 64), 2,
        "application/json", schema);

    static OperationProvenance Provenance(TypeIIPeriod period, Uuid root) => new(root, root, null,
        "StartTypeIIObservationPeriod", ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance lead"), period.Stream,
        "initial", null, new string('e', 64), Now, period.Stream, period.Stream);
}
