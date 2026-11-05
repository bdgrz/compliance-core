using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceSupportWithdrawalTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepOriginalAssertionGivenReasonedWithdrawal()
    {
        // Arrange
        var link = Link();
        var owner = Linked(link);
        var original = owner.Current;
        var view = owner.View();
        var request = Withdrawal(owner.Id);

        // Act
        var failure = owner.Withdraw(request, Actor(), Now.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, owner.Revision);
        Assert.False(owner.IsActive);
        Assert.Same(original, owner.Current);
        Assert.Equal(view, owner.View());
        var fact = Assert.IsType<EvidenceSupportWithdrawn>(Events(owner)[1]);
        Assert.Equal(owner.Id, fact.SubjectId);
        Assert.Equal(TenantId, fact.TenantId);
        Assert.Equal(ProgramId, fact.ProgramId);
        Assert.Equal(request.CommandId, fact.CommandId);
        Assert.Equal(1, fact.OriginalExpectedRevision);
        Assert.Equal(2, fact.Revision);
        Assert.Equal("Wrong target version", fact.Reason);
        Assert.Equal(MemberId, fact.ActorMemberId);
        Assert.Equal("Evidence lead", fact.ActorDisplay);
        Assert.Equal(Now.AddMinutes(1), fact.OccurredAt);
        Assert.Equal(64, fact.AuthoredIntentSha256.Length);
    }

    [Fact]
    public void ShouldReturnOriginalLinkWithoutAppendGivenAcceptedLinkRetryAfterWithdrawal()
    {
        // Arrange
        var link = Link();
        var owner = Linked(link);
        var original = owner.View();
        Assert.Null(owner.Withdraw(Withdrawal(owner.Id), Actor(), Now.AddMinutes(1)));

        // Act
        var failure = owner.Link(link, 99, new string('b', 64), Actor(), Now.AddDays(1));
        var replay = owner.Replay(link, Actor());

        // Assert
        Assert.Null(failure);
        Assert.True(replay!.Value.IsSuccess);
        Assert.Equal(original, replay.Value.Value);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, Events(owner).Length);
        Assert.False(owner.IsActive);
    }

    [Fact]
    public void ShouldKeepFirstWithdrawalGivenExactRetryWithLaterDisplayAndTime()
    {
        // Arrange
        var owner = Linked(Link());
        var request = Withdrawal(owner.Id);
        Assert.Null(owner.Withdraw(request, Actor(), Now.AddMinutes(1)));
        var original = owner.Withdrawal;

        // Act
        var retry = owner.Withdraw(request,
            new ActorReference("member", MemberId.ToString().ToUpperInvariant(), "Renamed lead"), Now.AddDays(1));

        // Assert
        Assert.Null(retry);
        Assert.Same(original, owner.Withdrawal);
        Assert.Equal(2, Events(owner).Length);
        Assert.Equal(2, owner.Revision);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("expected_revision")]
    [InlineData("command")]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("support")]
    public void ShouldRefuseChangedIntentWithoutAppendGivenWithdrawnSupport(string changed)
    {
        // Arrange
        var owner = Linked(Link());
        var request = Withdrawal(owner.Id);
        Assert.Null(owner.Withdraw(request, Actor(), Now.AddMinutes(1)));
        var candidate = changed switch
        {
            "reason" => request with { Reason = "Other reason" },
            "expected_revision" => request with { ExpectedRevision = 2 },
            "command" => request with { CommandId = Uuid.CreateVersion4() },
            "tenant" => request with { TenantId = Uuid.CreateVersion4() },
            "program" => request with { ProgramId = Uuid.CreateVersion4() },
            "support" => request with { SupportId = Uuid.CreateVersion4() },
            _ => request,
        };
        var actor = changed == "actor" ? ActorReference.ForMember(Uuid.CreateVersion4(), "Other lead") : Actor();

        // Act
        var failure = owner.Withdraw(candidate, actor, Now.AddDays(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, Events(owner).Length);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("long_reason")]
    [InlineData("command")]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("support")]
    [InlineData("service")]
    [InlineData("empty_member")]
    [InlineData("display")]
    [InlineData("time")]
    [InlineData("before_link")]
    public void ShouldRefuseInvalidWithdrawalWithoutFactGivenLinkedSupport(string invalid)
    {
        // Arrange
        var owner = Linked(Link());
        var request = Withdrawal(owner.Id);
        request = invalid switch
        {
            "reason" => request with { Reason = " " },
            "long_reason" => request with { Reason = new string('x', 2001) },
            "command" => request with { CommandId = Uuid.Empty },
            "tenant" => request with { TenantId = Uuid.CreateVersion4() },
            "program" => request with { ProgramId = Uuid.CreateVersion4() },
            "support" => request with { SupportId = Uuid.CreateVersion4() },
            _ => request,
        };
        var actor = invalid switch
        {
            "service" => new ActorReference("service", MemberId.ToString(), "Collector"),
            "empty_member" => ActorReference.ForMember(Uuid.Empty, "Lead"),
            "display" => ActorReference.ForMember(MemberId, " "),
            _ => Actor(),
        };
        var at = invalid switch { "time" => default, "before_link" => Now.AddSeconds(-1), _ => Now.AddMinutes(1) };

        // Act
        var failure = owner.Withdraw(request, actor, at);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.True(owner.IsActive);
        Assert.Null(owner.Withdrawal);
        Assert.Single(Events(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionAndMissingLinkWithoutFactGivenWithdrawal()
    {
        // Arrange
        var owner = Linked(Link());
        var empty = new EvidenceSupport(TenantId, ProgramId, owner.Id);

        // Act
        var stale = owner.Withdraw(Withdrawal(owner.Id) with { ExpectedRevision = 0 }, Actor(), Now);
        var missing = empty.Withdraw(Withdrawal(owner.Id), Actor(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(missing).Code);
        Assert.Single(Events(owner));
        Assert.Empty(Events(empty));
    }

    [Fact]
    public void ShouldReplayWithdrawalAndOriginalLinkGivenCompleteHistory()
    {
        // Arrange
        var link = Link();
        var owner = Linked(link);
        var request = Withdrawal(owner.Id);
        Assert.Null(owner.Withdraw(request, Actor(), Now.AddMinutes(1)));
        var history = Events(owner);

        // Act
        var replayed = new AggregateScenario<EvidenceSupport>(new EvidenceSupport(TenantId, ProgramId, owner.Id))
            .Given(history).Aggregate;
        var retry = replayed.Withdraw(request, Actor(), Now.AddDays(1));

        // Assert
        Assert.Null(retry);
        Assert.False(replayed.IsActive);
        Assert.Equal(owner.Withdrawal, replayed.Withdrawal);
        Assert.Equal(owner.View(), replayed.View());
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(Events(replayed));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("support")]
    [InlineData("revision")]
    [InlineData("expected_revision")]
    [InlineData("command")]
    [InlineData("reason")]
    [InlineData("actor")]
    [InlineData("digest")]
    [InlineData("time")]
    public void ShouldRejectInvalidWithdrawalHistoryGivenLinkedPredecessor(string invalid)
    {
        // Arrange
        var owner = Linked(Link());
        Assert.Null(owner.Withdraw(Withdrawal(owner.Id), Actor(), Now.AddMinutes(1)));
        var link = Events(owner)[0];
        var fact = Assert.IsType<EvidenceSupportWithdrawn>(Events(owner)[1]);
        fact = invalid switch
        {
            "tenant" => fact with { TenantId = Uuid.CreateVersion4() },
            "program" => fact with { ProgramId = Uuid.CreateVersion4() },
            "support" => fact with { SubjectId = Uuid.CreateVersion4() },
            "revision" => fact with { Revision = 3 },
            "expected_revision" => fact with { OriginalExpectedRevision = 0 },
            "command" => fact with { CommandId = Uuid.Empty },
            "reason" => fact with { Reason = " " },
            "actor" => fact with { ActorMemberId = Uuid.Empty },
            "digest" => fact with { AuthoredIntentSha256 = "invalid" },
            _ => fact with { OccurredAt = Now.AddSeconds(-1) },
        };
        var replay = new AggregateScenario<EvidenceSupport>(new EvidenceSupport(TenantId, ProgramId, owner.Id));

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => replay.Given(link, fact));
    }

    [Fact]
    public void ShouldRejectWithdrawalHistoryGivenMissingLinkOrRepeatedTerminalFact()
    {
        // Arrange
        var owner = Linked(Link());
        Assert.Null(owner.Withdraw(Withdrawal(owner.Id), Actor(), Now.AddMinutes(1)));
        var history = Events(owner);
        var withdrawal = Assert.IsType<EvidenceSupportWithdrawn>(history[1]);
        var missingPredecessor = withdrawal with { };
        var firstWithdrawal = withdrawal with { };
        var secondWithdrawal = withdrawal with { };
        var empty = new AggregateScenario<EvidenceSupport>(new EvidenceSupport(TenantId, ProgramId, owner.Id));
        var duplicate = new AggregateScenario<EvidenceSupport>(new EvidenceSupport(TenantId, ProgramId, owner.Id));

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => empty.Given(missingPredecessor));
        Assert.Throws<InvalidOperationException>(() => duplicate.Given(history[0], firstWithdrawal, secondWithdrawal));
    }

    static LinkEvidenceSupport Link() => new(Uuid.CreateVersion4(), TenantId, ProgramId, Uuid.CreateVersion4(),
        "control", Uuid.CreateVersion4(), 7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
        "Design evidence", "Exact target rationale", 0);

    static EvidenceSupport Linked(LinkEvidenceSupport request)
    {
        var owner = new EvidenceSupport(TenantId, ProgramId, request.CommandId);
        Assert.Null(owner.Link(request, 2, new string('a', 64), Actor(), Now));
        return owner;
    }

    static WithdrawEvidenceSupport Withdrawal(Uuid supportId) =>
        new(Uuid.CreateVersion4(), TenantId, ProgramId, supportId, 1, "Wrong target version");

    static ActorReference Actor() => ActorReference.ForMember(MemberId, "Evidence lead");

    static DomainEvent[] Events(EvidenceSupport owner) => new AggregateScenario<EvidenceSupport>(owner).PendingEvents.ToArray();
}
