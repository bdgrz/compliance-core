using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class MemberPersonLinkTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAdmitOnePendingPersonGivenUnboundMember()
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var member = Registered(command);
        var operation = Root(member, command);

        // Act
        var result = member.RequestPersonLink(operation, NativeFacts.Ref(), Person, 0);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Person, member.PendingPersonId);
        Assert.Null(member.LinkedPersonId);
        Assert.Equal(1, member.PersonLinkGeneration);
        var fact = Assert.IsType<MemberPersonLinkAdmitted>(Facts(member)[^1]);
        Assert.Equal(Person, fact.PersonId);
        Assert.Equal(command, fact.LinkId);
        Assert.Equal(operation.Actor, fact.Operation.Actor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRecordRefusalWithoutReplacingAdmissionGivenCompetingPerson(bool stale)
    {
        // Arrange
        var firstId = Uuid.CreateVersion4();
        var first = Registered(firstId);
        Assert.True(first.RequestPersonLink(Root(first, firstId), NativeFacts.Ref(), Person, 0).IsSuccess);
        var command = Uuid.CreateVersion4();
        var member = Replay(first, command);

        // Act
        var result = member.RequestPersonLink(Root(member, command), NativeFacts.Ref(), Uuid.CreateVersion4(), stale ? 0 : 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<MemberPersonLinkRefused>(Assert.Single(Facts(member)));
        Assert.Equal(Person, member.PendingPersonId);
        Assert.Null(member.LinkedPersonId);
        Assert.Equal(1, member.PersonLinkGeneration);
    }

    [Fact]
    public void ShouldReplayWithoutFactGivenExactSelectedAdmission()
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var member = Registered(command);
        var operation = Root(member, command);
        Assert.True(member.RequestPersonLink(operation, NativeFacts.Ref(), Person, 0).IsSuccess);
        var replayed = Replay(member, command);

        // Act
        var result = replayed.RequestPersonLink(operation, NativeFacts.Ref(), Person, 0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(Facts(replayed));
        Assert.Equal(Person, replayed.PendingPersonId);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenChangedIntentUnderAcceptedIdentity()
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var member = Registered(command);
        var operation = Root(member, command);
        Assert.True(member.RequestPersonLink(operation, NativeFacts.Ref(), Person, 0).IsSuccess);
        var replayed = Replay(member, command);
        var changed = operation with { AuthoredIntentSha256 = new string('c', 64) };

        // Act
        var result = replayed.RequestPersonLink(changed, NativeFacts.Ref(), Uuid.CreateVersion4(), 0);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(Facts(replayed));
        Assert.Equal(Person, replayed.PendingPersonId);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("cancel")]
    [InlineData("release")]
    public void ShouldRequireExactAdmissionGenerationGivenPersonConclusion(string conclusion)
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var member = Registered(command);
        Assert.True(member.RequestPersonLink(Root(member, command), NativeFacts.Ref(), Person, 0).IsSuccess);
        var receiver = Uuid.CreateVersion4();
        member = Replay(member, receiver);
        var link = Link(member, command) with { Generation = 99 };

        // Act
        var result = Conclude(member, receiver, link, conclusion);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(Facts(member));
        Assert.Equal(Person, member.PendingPersonId);
        Assert.Equal(1, member.PersonLinkGeneration);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("cancel")]
    [InlineData("release")]
    public void ShouldApplyOnlyVerifiedMatchingPersonConclusionGivenAdmission(string conclusion)
    {
        // Arrange
        var command = Uuid.CreateVersion4();
        var member = Registered(command);
        Assert.True(member.RequestPersonLink(Root(member, command), NativeFacts.Ref(), Person, 0).IsSuccess);
        var receiver = Uuid.CreateVersion4();
        member = Replay(member, receiver);
        var link = Link(member, command);

        // Act
        var result = Conclude(member, receiver, link, conclusion);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(member.PendingPersonId);
        Assert.Equal(conclusion == "confirm" ? (Uuid?)Person : null, member.LinkedPersonId);
        Assert.Equal(conclusion == "confirm" ? 1 : 2, member.PersonLinkGeneration);
        Assert.Single(Facts(member));
    }

    static Member Registered(Uuid command)
    {
        var member = new Member(Tenant, User, selectedCommandId: command);
        Assert.True(member.Register(NativeFacts.Root(member, Uuid.CreateVersion4(), "RegisterMember", null), NativeFacts.Ref()).IsSuccess);
        return member;
    }

    static OperationProvenance Root(Member member, Uuid command) =>
        NativeFacts.Root(member, command, "LinkPersonToMember", null, at: Now);

    static IReadOnlyList<DomainEvent> Facts(Member member) => new AggregateScenario<Member>(member).PendingEvents;

    static Member Replay(Member member, Uuid selected)
    {
        var facts = Facts(member).Select((fact, index) => DomainEventSeed.Attach(fact, member.Id, (ulong)index + 1)).ToArray();
        return NativeFacts.Replay(new Member(Tenant, User, selectedCommandId: selected), facts);
    }

    static PersonMemberLink Link(Member member, Uuid command)
    {
        var admission = Assert.IsType<MemberPersonLinkAdmitted>(member.PersonLinkAdmission);
        return new PersonMemberLink(User, member.Id, command, 1, admission.Metadata.EventId);
    }

    static Result Conclude(Member member, Uuid receiver, PersonMemberLink link, string conclusion)
    {
        var person = new Bdgrz.Compliance.Features.Workforce.Aggregates.Person(Tenant, Person);
        var operation = NativeFacts.Root(member, receiver, "MemberPersonLinkConclusion", null, at: Now);
        var sourceId = Uuid.CreateVersion4();
        operation = operation with { CausationEventId = sourceId };
        var header = new PersonHeader(Tenant, Person, 2, null, new string('a', 64), operation.Actor, Now);
        var sourceOperation = NativeFacts.Root(person, Uuid.CreateVersion4(), "PersonMemberLinkConclusion", 1, at: Now)
            with { RootOperationId = link.LinkId };
        return conclusion switch
        {
            "confirm" => member.ConfirmPersonLink(operation, NativeFacts.Ref(), Verified(person.Stream, sourceId,
                new PersonLinkedToMember(header, NativeFacts.Ref(), NativeFacts.Ref(), true, sourceOperation,
                    NativeFacts.Ref(), NativeFacts.Ref(), link))),
            "cancel" => member.CancelPersonLinkAdmission(operation, NativeFacts.Ref(), Verified(person.Stream, sourceId,
                new PersonMemberLinkRefused(header, NativeFacts.Ref(), NativeFacts.Ref(), false, sourceOperation,
                    NativeFacts.Ref(), NativeFacts.Ref(), link, "Declared refusal"))),
            "release" => member.ReleasePersonLink(operation, NativeFacts.Ref(), Verified(person.Stream, sourceId,
                new PersonUnlinkedFromMember(header, NativeFacts.Ref(), NativeFacts.Ref(), true, sourceOperation,
                    NativeFacts.Ref(), NativeFacts.Ref(), link))),
            _ => throw new ArgumentOutOfRangeException(nameof(conclusion)),
        };
    }

    static VerifiedSourceProof<T> Verified<T>(EventStreamAddress stream, Uuid eventId, T fact) where T : DomainEvent
    {
        DomainEventSeed.Attach(fact, Person, 2, eventId: eventId);
        return new VerifiedSourceProof<T>(stream, eventId, 1, 2, new string('a', 64), fact, 1, 1);
    }
}
