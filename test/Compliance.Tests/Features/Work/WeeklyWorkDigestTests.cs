using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WeeklyWorkDigestTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateOnly WeekStarting = new(2026, 10, 12);
    static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Scheduler = ActorReference.ForSystemProcess(
        "scheduler:WeeklyWorkDigestV1", "Weekly work digest scheduler");
    static readonly ActorReference DeliveryWorker = ActorReference.ForSystemProcess(
        "reactor:WeeklyWorkDigestDeliveryV1", "Weekly work digest delivery worker");

    [Fact]
    public void ShouldScheduleOneStableDigestGivenRepeatedMemberWeekSchedule()
    {
        // Arrange
        var digestId = WeeklyWorkDigestRules.IdFor(TenantId, MemberId, WeekStarting);
        var digest = new WeeklyWorkDigestRules(TenantId, digestId);

        // Act
        var scheduled = digest.Schedule(MemberId, WeekStarting, "schedule-2026-10-12",
            Scheduler, Now);
        var retry = digest.Schedule(MemberId, WeekStarting, "schedule-2026-10-12",
            Scheduler, Now.AddMinutes(1));
        var changedSchedule = digest.Schedule(MemberId, WeekStarting, "different-schedule",
            Scheduler, Now);

        // Assert
        Assert.True(scheduled.IsSuccess, scheduled.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changedSchedule.Error?.Kind);
        Assert.Equal("scheduled", digest.Read().Status);
        Assert.Equal(WeekStarting, digest.Read().WeekStarting);
        Assert.Empty(digest.Read().DeliveryAttempts);
        Assert.Single(digest.PendingEvents);
    }

    [Fact]
    public void ShouldAllowOnlyDeterministicRetryAfterFailedDeliveryGivenWeeklyDigest()
    {
        // Arrange
        var digest = NewScheduledDigest();
        var firstKey = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 1);
        var secondKey = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 2);

        // Act
        var firstRequest = digest.RequestDelivery(1, 1, firstKey, DeliveryWorker, Now.AddMinutes(1));
        var duplicateRequest = digest.RequestDelivery(1, 1, firstKey, DeliveryWorker,
            Now.AddMinutes(2));
        var failed = digest.RecordDeliveryResult(2, 1, firstKey, "failed", "provider_unavailable",
            DeliveryWorker, Now.AddMinutes(3));
        var retry = digest.RequestDelivery(3, 2, secondKey, DeliveryWorker, Now.AddMinutes(4));
        var success = digest.RecordDeliveryResult(4, 2, secondKey, "delivered", null,
            DeliveryWorker, Now.AddMinutes(5));
        var duplicateSuccess = digest.RequestDelivery(5, 3,
            WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 3), DeliveryWorker, Now.AddMinutes(6));

        // Assert
        Assert.True(firstRequest.IsSuccess, firstRequest.Error?.Message);
        Assert.True(duplicateRequest.IsSuccess, duplicateRequest.Error?.Message);
        Assert.True(failed.IsSuccess, failed.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.True(success.IsSuccess, success.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, duplicateSuccess.Error?.Kind);
        var state = digest.Read();
        Assert.Equal("delivered", state.Status);
        Assert.Collection(state.DeliveryAttempts,
            first =>
            {
                Assert.Equal("failed", first.Status);
                Assert.Equal("provider_unavailable", first.FailureCode);
            },
            second =>
            {
                Assert.Equal("delivered", second.Status);
                Assert.Null(second.FailureCode);
                Assert.Equal(DeliveryWorker, second.RecordedBy);
            });
    }

    [Fact]
    public void ShouldAcceptExactCallbackReplayGivenALaterRetryHasStarted()
    {
        // Arrange
        var digest = NewScheduledDigest();
        var firstKey = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 1);
        var secondKey = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 2);
        Assert.True(digest.RequestDelivery(1, 1, firstKey, DeliveryWorker, Now.AddMinutes(1)).IsSuccess);
        Assert.True(digest.RecordDeliveryResult(2, 1, firstKey, "failed", "provider_unavailable",
            DeliveryWorker, Now.AddMinutes(2)).IsSuccess);
        Assert.True(digest.RequestDelivery(3, 2, secondKey, DeliveryWorker, Now.AddMinutes(3)).IsSuccess);

        // Act
        var replay = digest.RecordDeliveryResult(2, 1, firstKey, "failed", "provider_unavailable",
            DeliveryWorker, Now.AddMinutes(2));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(4, digest.Revision);
        Assert.Collection(digest.Read().DeliveryAttempts,
            first => Assert.Equal("failed", first.Status),
            second => Assert.Equal("requested", second.Status));
        Assert.Single(digest.PendingEvents,
            ev => ev is WeeklyWorkDigestDeliveryRecorded { AttemptNumber: 1 });
    }

    [Fact]
    public void ShouldReplayDeliveryHistoryGivenScheduledAndCompletedEvents()
    {
        // Arrange
        var digest = NewScheduledDigest();
        var key = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 1);
        Assert.True(digest.RequestDelivery(1, 1, key, DeliveryWorker, Now.AddMinutes(1)).IsSuccess);
        Assert.True(digest.RecordDeliveryResult(2, 1, key, "delivered", null,
            DeliveryWorker, Now.AddMinutes(2)).IsSuccess);
        var events = digest.PendingEvents.ToArray();

        // Act
        var replay = NewEmptyDigest(digest.Id);
        foreach (var source in events)
            replay.Replay(source);

        // Assert
        var expected = digest.Read();
        var actual = replay.Read();
        Assert.Equal(expected.TenantId, actual.TenantId);
        Assert.Equal(expected.DigestId, actual.DigestId);
        Assert.Equal(expected.MemberId, actual.MemberId);
        Assert.Equal(expected.WeekStarting, actual.WeekStarting);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.ScheduleKey, actual.ScheduleKey);
        Assert.Equal(expected.ScheduledAt, actual.ScheduledAt);
        Assert.Equal(expected.ScheduledBy, actual.ScheduledBy);
        Assert.Equal(expected.DeliveryAttempts, actual.DeliveryAttempts);
    }

    [Fact]
    public void ShouldSuppressFurtherAttemptsGivenACompletedFailureAndCurrentOptOut()
    {
        // Arrange
        var digest = NewScheduledDigest();
        var key = WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 1);
        Assert.True(digest.RequestDelivery(1, 1, key, DeliveryWorker, Now.AddMinutes(1)).IsSuccess);
        Assert.True(digest.RecordDeliveryResult(2, 1, key, "failed", "provider_unavailable",
            DeliveryWorker, Now.AddMinutes(2)).IsSuccess);

        // Act
        var suppressed = digest.SuppressDelivery(3, "email_digest_opted_out", DeliveryWorker,
            Now.AddMinutes(3));
        var thirdAttempt = digest.RequestDelivery(4, 2,
            WeeklyWorkDigestRules.DeliveryKeyFor(digest.Id, 2), DeliveryWorker, Now.AddMinutes(4));

        // Assert
        Assert.True(suppressed.IsSuccess, suppressed.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, thirdAttempt.Error?.Kind);
        Assert.Equal("suppressed", digest.Read().Status);
        Assert.Equal("email_digest_opted_out", digest.Read().SuppressionReason);
    }

    [Fact]
    public void ShouldRejectInvalidScheduleAndUnattributedDeliveryGivenNewDigest()
    {
        // Arrange
        var digest = NewEmptyDigest(WeeklyWorkDigestRules.IdFor(TenantId, MemberId, WeekStarting));
        var wrongWeek = WeekStarting.AddDays(1);
        var anonymous = ActorReference.ForMember(Uuid.Empty, "");

        // Act
        var nonMonday = digest.Schedule(MemberId, wrongWeek, "schedule-2026-10-12", Scheduler, Now);
        var missingActor = digest.Schedule(MemberId, WeekStarting, "schedule-2026-10-12",
            anonymous, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, nonMonday.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, missingActor.Error?.Kind);
        Assert.Equal(0, digest.Read().Revision);
        Assert.Empty(digest.PendingEvents);
    }

    static WeeklyWorkDigestRules NewScheduledDigest()
    {
        var digest = NewEmptyDigest(WeeklyWorkDigestRules.IdFor(TenantId, MemberId, WeekStarting));
        Assert.True(digest.Schedule(MemberId, WeekStarting, "schedule-2026-10-12",
            Scheduler, Now).IsSuccess);
        return digest;
    }

    static WeeklyWorkDigestRules NewEmptyDigest(Uuid digestId) => new(TenantId, digestId);
}
