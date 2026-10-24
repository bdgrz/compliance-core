using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailChallengeDeliveryRecordTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldNameAttemptsDeterministicallyGivenTheSameChallenge()
    {
        // Arrange
        var challengeId = Uuid.CreateVersion4();

        // Act
        var first = EmailChallengeDeliveryRecord.Empty(challengeId, "verification").WithNextAttempt(Now);
        var again = EmailChallengeDeliveryRecord.Empty(challengeId, "verification").WithNextAttempt(Now.AddHours(1));

        // Assert
        Assert.Equal(first.Latest!.AttemptId, again.Latest!.AttemptId);
        Assert.NotEqual(first.Latest.AttemptId, EmailChallengeDeliveryRecord.AttemptIdOf(challengeId, 2));
        Assert.NotEqual(first.Latest.AttemptId,
            EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification").WithNextAttempt(Now).Latest!.AttemptId);
    }

    [Fact]
    public void ShouldRefuseAnotherAttemptGivenAnOpenAttempt()
    {
        // Arrange
        var record = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification").WithNextAttempt(Now);

        // Act
        var failure = Record.Exception(() => record.WithNextAttempt(Now));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(record.HasOpenAttempt);
        Assert.Equal("pending", record.Status);
    }

    [Fact]
    public void ShouldCloseTheOpenLatestAttemptGivenItsOutcome()
    {
        // Arrange
        var record = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification").WithNextAttempt(Now);

        // Act
        var decision = record.Decide(record.Latest!.AttemptId, EmailChallengeDeliveryAttempt.Sent, null,
            Uuid.CreateVersion4(), Now.AddSeconds(1));

        // Assert
        Assert.Equal(EmailChallengeOutcomeKind.Closed, decision.Kind);
        Assert.Equal("delivered", decision.Record.Status);
        Assert.False(decision.Record.HasOpenAttempt);
    }

    [Fact]
    public void ShouldIgnoreAnOutcomeGivenANewerAttempt()
    {
        // Arrange
        var empty = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification");
        var first = empty.WithNextAttempt(Now);
        var closed = first.Decide(first.Latest!.AttemptId, EmailChallengeFailureCertainty.Unknown, "transport_ambiguous",
            Uuid.CreateVersion4(), Now).Record;
        var second = closed.WithNextAttempt(Now.AddSeconds(30));

        // Act
        var late = second.Decide(first.Latest.AttemptId, EmailChallengeDeliveryAttempt.Sent, null, Uuid.CreateVersion4(), Now);

        // Assert
        Assert.Equal(EmailChallengeOutcomeKind.Stale, late.Kind);
        Assert.Equal(second, late.Record);
    }

    [Theory]
    [InlineData(EmailChallengeFailureCertainty.DefiniteTransient, EmailChallengeDeliveryAttempt.Sent)]
    [InlineData(EmailChallengeFailureCertainty.DefinitePermanent, EmailChallengeDeliveryAttempt.Sent)]
    [InlineData(EmailChallengeDeliveryAttempt.Sent, EmailChallengeFailureCertainty.DefiniteTransient)]
    public void ShouldConflictGivenAContradictoryOutcomeForAClosedLatestAttempt(string recorded, string contradiction)
    {
        // Arrange
        var open = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification").WithNextAttempt(Now);
        var closed = open.Decide(open.Latest!.AttemptId, recorded, recorded == EmailChallengeDeliveryAttempt.Sent ? null : "code",
            Uuid.CreateVersion4(), Now).Record;

        // Act
        var decision = closed.Decide(open.Latest.AttemptId, contradiction,
            contradiction == EmailChallengeDeliveryAttempt.Sent ? null : "code", Uuid.CreateVersion4(), Now);

        // Assert
        Assert.Equal(EmailChallengeOutcomeKind.Conflict, decision.Kind);
        Assert.Equal(closed, decision.Record);
    }

    [Fact]
    public void ShouldReplayTheSameOutcomeGivenAnExactRepeat()
    {
        // Arrange
        var open = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification").WithNextAttempt(Now);
        var closed = open.Decide(open.Latest!.AttemptId, EmailChallengeFailureCertainty.DefinitePermanent, "code",
            Uuid.CreateVersion4(), Now).Record;

        // Act
        var decision = closed.Decide(open.Latest.AttemptId, EmailChallengeFailureCertainty.DefinitePermanent, "code",
            Uuid.CreateVersion4(), Now.AddMinutes(1));

        // Assert
        Assert.Equal(EmailChallengeOutcomeKind.Replayed, decision.Kind);
        Assert.Equal("failed", decision.Record.Status);
    }

    [Fact]
    public void ShouldFailOnlyGivenAPermanentRejectionOrASpentBudget()
    {
        // Arrange
        var record = EmailChallengeDeliveryRecord.Empty(Uuid.CreateVersion4(), "verification");
        var statuses = new List<string>();

        // Act
        for (var attempt = 1; attempt <= EmailChallengeDeliveryRecord.MaximumAttempts; attempt++)
        {
            record = record.WithNextAttempt(Now);
            record = record.Decide(record.Latest!.AttemptId, EmailChallengeFailureCertainty.Unknown, "transport_ambiguous",
                Uuid.CreateVersion4(), Now).Record;
            statuses.Add(record.Status);
        }

        // Assert
        Assert.Equal(["pending", "pending", "pending", "pending", "failed"], statuses);
        Assert.Throws<InvalidOperationException>(() => record.WithNextAttempt(Now));
    }
}
