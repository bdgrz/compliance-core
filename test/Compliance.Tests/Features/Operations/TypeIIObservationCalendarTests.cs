using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIObservationCalendarTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ObservationRange Year = new(new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30));
    static readonly ImmutableContentRef Intent = new(Uuid.Parse("00000000-0000-4000-8000-0000000000a1",
        System.Globalization.CultureInfo.InvariantCulture), new string('a', 64), 2, "application/json",
        "type-i-i-range-intent.v1");

    [Fact]
    public void ShouldRefuseOverlapGivenInclusiveEndpointsOfAnotherPeriodRange()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        Authorize(calendar, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Year);

        // Act
        var sharedLastDay = Refusal(calendar, new ObservationRange(Year.UntilInclusive, Year.UntilInclusive.AddDays(30)));
        var sharedFirstDay = Refusal(calendar, new ObservationRange(Year.From.AddDays(-30), Year.From));
        var adjacentAfter = Refusal(calendar, new ObservationRange(Year.UntilInclusive.AddDays(1), Year.UntilInclusive.AddDays(30)));
        var adjacentBefore = Refusal(calendar, new ObservationRange(Year.From.AddDays(-30), Year.From.AddDays(-1)));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, sharedLastDay?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, sharedFirstDay?.Kind);
        Assert.Null(adjacentAfter);
        Assert.Null(adjacentBefore);
    }

    [Fact]
    public void ShouldHoldOldAndNewRangeUntilActivatedGivenAmendmentOfOnePeriod()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        var (period, opening, amendment) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var moved = new ObservationRange(Year.From.AddMonths(1), Year.UntilInclusive.AddMonths(1));
        Authorize(calendar, period, opening, Year);
        Settle(calendar, period, TypeIIRangeSettlementKind.Activated, opening, null);

        // Act
        var authorized = Authorize(calendar, period, amendment, moved, opening);
        var oldHeld = Refusal(calendar, Year);
        var newHeld = Refusal(calendar, moved);
        Settle(calendar, period, TypeIIRangeSettlementKind.Activated, amendment, opening);
        var oldReleased = Refusal(calendar, new ObservationRange(Year.From, Year.From.AddDays(10)));
        var newStillHeld = Refusal(calendar, moved);

        // Assert
        Assert.True(authorized.IsSuccess, authorized.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, oldHeld?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, newHeld?.Kind);
        Assert.Null(oldReleased);
        Assert.Equal(RequestErrorKind.Conflict, newStillHeld?.Kind);
        Assert.Equal(TypeIIRangeReservationState.Released, calendar.Find(opening)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(amendment)!.State);
        Assert.Equal([1L, 2L], new[] { calendar.Find(opening)!.Generation, calendar.Find(amendment)!.Generation });
    }

    [Fact]
    public void ShouldReleaseOnlyPendingRangeGivenCanceledAmendmentAndKeepOldActive()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        var (period, opening, amendment) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var moved = new ObservationRange(Year.From.AddMonths(1), Year.UntilInclusive.AddMonths(1));
        Authorize(calendar, period, opening, Year);
        Settle(calendar, period, TypeIIRangeSettlementKind.Activated, opening, null);
        Authorize(calendar, period, amendment, moved, opening);

        // Act
        var released = Settle(calendar, period, TypeIIRangeSettlementKind.Released, amendment, null);
        var oldHeld = Refusal(calendar, Year);
        var newFree = Refusal(calendar, new ObservationRange(moved.UntilInclusive.AddDays(1).AddMonths(1), moved.UntilInclusive.AddMonths(2)));
        var newReleased = Refusal(calendar, new ObservationRange(Year.UntilInclusive.AddDays(1), moved.UntilInclusive));

        // Assert
        Assert.True(released.IsSuccess, released.Error?.Message);
        Assert.Equal(TypeIIRangeReservationState.Active, calendar.Find(opening)!.State);
        Assert.Equal(TypeIIRangeReservationState.Released, calendar.Find(amendment)!.State);
        Assert.Equal(RequestErrorKind.Conflict, oldHeld?.Kind);
        Assert.Null(newFree);
        Assert.Null(newReleased);
    }

    [Fact]
    public void ShouldRefuseLateAcquisitionGivenOperationFencedBeforeAnyGrant()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        var (period, operation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var fenced = Settle(calendar, period, TypeIIRangeSettlementKind.Fenced, operation, null);
        var late = Authorize(calendar, period, operation, Year, tryOnly: true);

        // Assert
        Assert.True(fenced.IsSuccess, fenced.Error?.Message);
        Assert.Equal(TypeIIRangeReservationState.Fenced, calendar.Find(operation)!.State);
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.Equal(0L, calendar.ReservationGeneration);
    }

    [Fact]
    public void ShouldRefuseSettlementGivenAnotherPeriodOrAlreadyReleasedReservation()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        var (period, other, operation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Authorize(calendar, period, operation, Year);

        // Act
        var wrongPeriod = Settle(calendar, other, TypeIIRangeSettlementKind.Released, operation, null);
        var released = Settle(calendar, period, TypeIIRangeSettlementKind.Released, operation, null);
        var again = Settle(calendar, period, TypeIIRangeSettlementKind.Released, operation, null);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongPeriod.Error?.Kind);
        Assert.True(released.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
    }

    [Fact]
    public void ShouldRebuildReservationsFromOrderedFactsGivenReplay()
    {
        // Arrange
        var calendar = new TypeIICalendarRules(TenantId, ProgramId);
        var (period, opening, amendment) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var moved = new ObservationRange(Year.From.AddMonths(1), Year.UntilInclusive.AddMonths(1));
        Authorize(calendar, period, opening, Year);
        Settle(calendar, period, TypeIIRangeSettlementKind.Activated, opening, null);
        Authorize(calendar, period, amendment, moved, opening);
        Settle(calendar, period, TypeIIRangeSettlementKind.Activated, amendment, opening);
        var events = calendar.AppliedChanges.ToArray();

        // Act
        var replay = new TypeIICalendarRules(TenantId, ProgramId);
        foreach (var change in events)
            replay.Apply(change);

        // Assert
        Assert.Equal(calendar.Revision, replay.Revision);
        Assert.Equal(calendar.ReservationGeneration, replay.ReservationGeneration);
        Assert.Equal(TypeIIRangeReservationState.Released, replay.Find(opening)!.State);
        Assert.Equal(TypeIIRangeReservationState.Active, replay.Find(amendment)!.State);
        Assert.Equal(moved, replay.Find(amendment)!.Range);
    }

    static RequestError? Refusal(TypeIICalendarRules calendar, ObservationRange range) =>
        calendar.FindOverlap(Uuid.CreateVersion4(), range) is null
            ? null
            : new RequestError(RequestErrorKind.Conflict, "overlap");

    static Result Authorize(TypeIICalendarRules calendar, Uuid period, Uuid operation, ObservationRange range,
        Uuid? prior = null, bool tryOnly = false)
    {
        var kind = prior is null ? TypeIIRangeOperationKind.Opening : TypeIIRangeOperationKind.Amendment;
        var authorized = calendar.Authorize(Provenance(calendar, operation), Intent, period, kind, range, prior);
        Assert.True(tryOnly || authorized.IsSuccess, authorized.Error?.Message);
        return authorized;
    }

    static Result Settle(TypeIICalendarRules calendar, Uuid period, TypeIIRangeSettlementKind kind, Uuid operation,
        Uuid? released) => calendar.Settle(Provenance(calendar, Uuid.CreateVersion4(), operation), Intent, period, kind,
        operation, released);

    static OperationProvenance Provenance(TypeIICalendarRules calendar, Uuid operation) =>
        Provenance(calendar, Uuid.CreateVersion4(), operation);

    static OperationProvenance Provenance(TypeIICalendarRules calendar, Uuid logicalId, Uuid root) => new(logicalId,
        root, null, "Synthetic", ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance lead"), calendar.Stream,
        "initial", null, new string('b', 64), At, calendar.Stream, calendar.Stream);
}
