using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIIObservationCalendarTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateOnly PeriodStart = new(2026, 10, 1);
    static readonly DateOnly PeriodEnd = new(2027, 9, 30);
    static readonly DateTimeOffset ReservedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainExactReservationGivenFirstPeriodStart()
    {
        // Arrange
        var periodId = Uuid.CreateVersion4();
        var planId = Uuid.CreateVersion4();
        var calendar = new TypeIIObservationCalendar(TenantId, ProgramId);

        // Act
        var failure = calendar.Reserve(periodId, Uuid.CreateVersion4(), planId,
            PeriodStart, PeriodEnd, Hash('a'), ActorId, "Compliance lead", ReservedAt);

        // Assert
        Assert.Null(failure);
        var reservation = Assert.Single(calendar.Current!.Periods);
        Assert.Equal(periodId, reservation.PeriodId);
        Assert.Equal(planId, reservation.PlanId);
        Assert.Equal(PeriodStart, reservation.PeriodStart);
        Assert.Equal(PeriodEnd, reservation.PeriodEnd);
        Assert.Equal(Hash('a'), reservation.OpeningSnapshotSha256);
        Assert.Equal(1, calendar.Revision);
    }

    [Fact]
    public void ShouldRejectOverlappingPeriodGivenExistingProgramReservation()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = Uuid.CreateVersion4();

        // Act
        var failure = calendar.Reserve(periodId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            PeriodStart.AddMonths(6), PeriodEnd.AddMonths(6), Hash('b'), ActorId,
            "Compliance lead", ReservedAt.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, failure?.Code);
        Assert.Single(calendar.Current!.Periods);
        Assert.Equal(1, calendar.Revision);
    }

    [Fact]
    public void ShouldAllowAdjacentPeriodsGivenNonOverlappingProgramBoundaries()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);

        // Act
        var failure = calendar.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), PeriodEnd.AddDays(1), PeriodEnd.AddYears(1), Hash('b'),
            ActorId, "Compliance lead", ReservedAt.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, calendar.Current!.Periods.Count);
    }

    [Fact]
    public void ShouldTreatSameReservationAsIdempotentGivenLaterRetryTime()
    {
        // Arrange
        var periodId = Uuid.CreateVersion4();
        var examinationId = Uuid.CreateVersion4();
        var planId = Uuid.CreateVersion4();
        var calendar = new TypeIIObservationCalendar(TenantId, ProgramId);
        Assert.Null(calendar.Reserve(periodId, examinationId, planId, PeriodStart, PeriodEnd,
            Hash('a'), ActorId, "Compliance lead", ReservedAt));

        // Act
        var retry = calendar.Reserve(periodId, examinationId, planId, PeriodStart, PeriodEnd,
            Hash('a'), ActorId, "Compliance lead", ReservedAt.AddHours(1));

        // Assert
        Assert.Null(retry);
        Assert.Single(calendar.Current!.Periods);
        Assert.Equal(1, calendar.Revision);
        Assert.Equal(ReservedAt, calendar.Current.Periods[0].CreatedAt);
    }

    [Fact]
    public void ShouldKeepOriginalRangeUntilReservedAmendmentCompletesGivenProgramReservation()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = calendar.Current!.Periods[0].PeriodId;
        var amendedStart = PeriodStart.AddDays(-30);
        var amendedEnd = PeriodEnd.AddDays(-30);

        // Act
        var reserved = calendar.BeginAmendment(periodId, amendedStart, amendedEnd,
            "Align with the approved testing window", ActorId, "Compliance lead",
            ReservedAt.AddMinutes(1));

        // Assert
        Assert.Null(reserved);
        var pending = Assert.Single(calendar.Current!.Periods);
        Assert.Equal(PeriodStart, pending.PeriodStart);
        Assert.Equal(PeriodEnd, pending.PeriodEnd);
        Assert.Equal(amendedStart, pending.PendingAmendment!.PeriodStart);
        Assert.Equal(amendedEnd, pending.PendingAmendment.PeriodEnd);

        // Act
        var completed = calendar.CompleteAmendment(periodId, amendedStart, amendedEnd,
            "Align with the approved testing window", ActorId, "Compliance lead",
            ReservedAt.AddMinutes(2));

        // Assert
        Assert.Null(completed);
        var amended = Assert.Single(calendar.Current!.Periods);
        Assert.Equal(amendedStart, amended.PeriodStart);
        Assert.Equal(amendedEnd, amended.PeriodEnd);
        Assert.Null(amended.PendingAmendment);
        Assert.Equal(amendedEnd, amended.LastAmendment!.PeriodEnd);
        Assert.Equal(3, calendar.Revision);
    }

    [Fact]
    public void ShouldReservePendingAmendmentRangeAgainstNewPeriodsGivenPendingAmendment()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = calendar.Current!.Periods[0].PeriodId;
        var amendedStart = PeriodEnd.AddDays(1);
        var amendedEnd = PeriodEnd.AddYears(1);
        Assert.Null(calendar.BeginAmendment(periodId, amendedStart, amendedEnd,
            "Align with the approved testing window", ActorId, "Compliance lead",
            ReservedAt.AddMinutes(1)));

        // Act
        var failure = calendar.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), amendedStart, amendedEnd, Hash('b'), ActorId,
            "Compliance lead", ReservedAt.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, failure?.Code);
        Assert.Single(calendar.Current!.Periods);
        Assert.Equal(2, calendar.Revision);
    }

    [Fact]
    public void ShouldCancelFailedAmendmentReservationAndAllowRangeReuseGivenPendingAmendment()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = calendar.Current!.Periods[0].PeriodId;
        var amendedStart = PeriodEnd.AddDays(1);
        var amendedEnd = PeriodEnd.AddYears(1);
        const string reason = "Align with the approved testing window";
        Assert.Null(calendar.BeginAmendment(periodId, amendedStart, amendedEnd, reason,
            ActorId, "Compliance lead", ReservedAt.AddMinutes(1)));

        // Act
        var cancelled = calendar.CancelAmendment(periodId, amendedStart, amendedEnd, reason,
            ActorId, "Compliance lead", ReservedAt.AddMinutes(2));
        var reserve = calendar.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), amendedStart, amendedEnd, Hash('b'), ActorId,
            "Compliance lead", ReservedAt.AddMinutes(3));

        // Assert
        Assert.Null(cancelled);
        Assert.Null(reserve);
        Assert.Null(calendar.Current!.Periods.Single(item => item.PeriodId == periodId)
            .PendingAmendment);
        Assert.Equal(4, calendar.Revision);
        Assert.Equal(2, calendar.Current.Periods.Count);
    }

    [Fact]
    public void ShouldRejectAmendmentThatOverlapsAnotherPeriodGivenExistingReservation()
    {
        // Arrange
        var calendar = new TypeIIObservationCalendar(TenantId, ProgramId);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.Null(calendar.Reserve(first, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            PeriodStart, PeriodEnd, Hash('a'), ActorId, "Compliance lead", ReservedAt));
        Assert.Null(calendar.Reserve(second, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            PeriodEnd.AddYears(1).AddDays(1), PeriodEnd.AddYears(2), Hash('b'), ActorId,
            "Compliance lead", ReservedAt.AddMinutes(1)));

        // Act
        var failure = calendar.BeginAmendment(second, PeriodEnd, PeriodEnd.AddYears(1),
            "Align with the approved testing window", ActorId, "Compliance lead",
            ReservedAt.AddMinutes(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, failure?.Code);
        Assert.Null(calendar.Current!.Periods.Single(item => item.PeriodId == second)
            .PendingAmendment);
        Assert.Equal(2, calendar.Revision);
    }

    [Fact]
    public void ShouldReplayReservedAndCompletedAmendmentGivenCommittedCalendarEvents()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = calendar.Current!.Periods[0].PeriodId;
        var amendedStart = PeriodStart.AddDays(-30);
        var amendedEnd = PeriodEnd.AddDays(-30);
        const string reason = "Align with the approved testing window";
        Assert.Null(calendar.BeginAmendment(periodId, amendedStart, amendedEnd, reason,
            ActorId, "Compliance lead", ReservedAt.AddMinutes(1)));
        Assert.Null(calendar.CompleteAmendment(periodId, amendedStart, amendedEnd, reason,
            ActorId, "Compliance lead", ReservedAt.AddMinutes(2)));
        var events = new AggregateScenario<TypeIIObservationCalendar>(calendar).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<TypeIIObservationCalendar>(
            new TypeIIObservationCalendar(TenantId, ProgramId)).Given(events).Aggregate;

        // Assert
        Assert.Equal(calendar.Revision, replay.Revision);
        var original = Assert.Single(calendar.Current!.Periods);
        var replayed = Assert.Single(replay.Current!.Periods);
        Assert.Equal(original.PeriodStart, replayed.PeriodStart);
        Assert.Equal(original.PeriodEnd, replayed.PeriodEnd);
        Assert.Equal(original.LastAmendment, replayed.LastAmendment);
        Assert.Null(replayed.PendingAmendment);
    }

    [Fact]
    public void ShouldAllowDateReuseAfterClosingPeriodGivenReleasedReservation()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var closedPeriodId = calendar.Current!.Periods[0].PeriodId;

        // Act
        var released = calendar.Release(closedPeriodId, Uuid.CreateVersion4(), Hash('c'),
            ActorId, "Compliance lead", ReservedAt.AddDays(1));
        var reused = calendar.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), PeriodStart, PeriodEnd, Hash('b'), ActorId,
            "Compliance lead", ReservedAt.AddDays(2));

        // Assert
        Assert.Null(released);
        Assert.Null(reused);
        Assert.Equal(3, calendar.Revision);
        Assert.False(calendar.Current!.Periods.Single(item =>
            item.PeriodId == closedPeriodId).IsActive);
        Assert.True(calendar.Current.Periods.Single(item =>
            item.PeriodId != closedPeriodId).IsActive);
    }

    [Fact]
    public void ShouldReplayReservationReleaseGivenFrozenCloseSnapshot()
    {
        // Arrange
        var calendar = CalendarWithReservation(PeriodStart, PeriodEnd);
        var periodId = calendar.Current!.Periods[0].PeriodId;
        var closeSnapshotId = Uuid.CreateVersion4();
        Assert.Null(calendar.Release(periodId, closeSnapshotId, Hash('c'), ActorId,
            "Compliance lead", ReservedAt.AddDays(1)));
        var events = new AggregateScenario<TypeIIObservationCalendar>(calendar).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<TypeIIObservationCalendar>(
            new TypeIIObservationCalendar(TenantId, ProgramId)).Given(events).Aggregate;

        // Assert
        Assert.Equal(2, replay.Revision);
        Assert.False(Assert.Single(replay.Current!.Periods).IsActive);
    }

    static TypeIIObservationCalendar CalendarWithReservation(DateOnly start, DateOnly end)
    {
        var calendar = new TypeIIObservationCalendar(TenantId, ProgramId);
        Assert.Null(calendar.Reserve(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), start, end, Hash('a'), ActorId, "Compliance lead", ReservedAt));
        return calendar;
    }

    static string Hash(char value) => new(value, 64);
}
