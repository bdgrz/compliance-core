using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectChangedContentGivenRecordedPerson()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record(" Ada Lovelace ", " ada@example.com ", Author, Now).IsSuccess);

        // Act
        var replay = person.Record("Ada Lovelace", "ada@example.com", Author, Now);
        var changed = person.Record("Ada King", "ada@example.com", Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(person.Id, replay.Value.PersonId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        var recorded = Assert.IsType<PersonRecorded>(
            Assert.Single(person.PendingEvents));
        Assert.Equal("Ada Lovelace", recorded.DisplayName);
        Assert.Equal("ada@example.com", recorded.WorkEmail);
        Assert.Equal(Author, recorded.Actor);
    }

    [Fact]
    public void ShouldRecordRestrictedPersonalContactGivenValidPerson()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        var contact = new PersonalContactDetails("ada.personal@example.com", "+1 202 555 0147");

        // Act
        var result = person.Record("Ada Lovelace", "ada@example.com", contact, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<PersonRecorded>(
            Assert.Single(person.PendingEvents));
        Assert.Equal(contact, recorded.PersonalContact);
        Assert.Equal(Author, recorded.Actor);
        var changedReplay = person.Record("Ada Lovelace", "ada@example.com",
            new PersonalContactDetails("different@example.com", contact.PersonalPhone), Author, Now);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changedReplay.Error).Kind);
    }

    [Fact]
    public void ShouldAdvanceRevisionAndRejectStaleOrMissingGivenRevise()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        var missing = new PersonRules(TenantId, Uuid.CreateVersion4());
        var editor = ActorReference.ForMember(Uuid.CreateVersion4(), "Editor");
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);

        // Act
        var revised = person.Revise(1, "Ada King", "ada@example.com", editor, Now.AddMinutes(1));
        var stale = person.Revise(1, "Stale", null, editor, Now.AddMinutes(2));
        var absent = missing.Revise(1, "Nobody", null, editor, Now);

        // Assert
        Assert.Null(revised);
        Assert.Equal(2, person.Revision);
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
        var ev = Assert.IsType<PersonRevised>(
            person.PendingEvents[1]);
        Assert.Equal(2, ev.Revision);
        Assert.Equal(editor, ev.Actor);
    }

    [Theory]
    [InlineData(" ", null)]
    [InlineData("Ada", "not-an-email")]
    [InlineData("Ada", "@example.com")]
    [InlineData("Ada", "ada@")]
    [InlineData("Ada", "ada lovelace@example.com")]
    public void ShouldRejectBeforeAppendGivenInvalidPerson(string displayName, string? workEmail)
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());

        // Act
        var result = person.Record(displayName, workEmail, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(person.IsCreated);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    public void ShouldRejectGivenOversizeDisplayName()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());

        // Act
        var result = person.Record(new string('a', 201), null, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldCorrelateAndClearMembershipGivenCurrentRevision()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        var userId = Uuid.CreateVersion4();

        // Act
        var linked = person.CorrelateMembership(1, userId, Author, Now);
        var unchanged = person.CorrelateMembership(2, userId, Author, Now);
        var cleared = person.CorrelateMembership(2, null, Author, Now);

        // Assert
        Assert.Null(linked);
        Assert.Null(unchanged);
        Assert.Null(cleared);
        Assert.Equal(3, person.Revision);
        Assert.Null(person.CorrelatedUserId);
        var events = person.PendingEvents
            .OfType<PersonMembershipCorrelated>().ToList();
        Assert.Equal([userId, (Uuid?)null], events.Select(ev => ev.UserId));
        Assert.Equal([2L, 3L], events.Select(ev => ev.Revision));
        Assert.All(events, ev => Assert.Equal(Author, ev.Actor));
    }

    [Fact]
    public void ShouldRejectCorrelationGivenStaleRevisionOrMissingPerson()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        var absent = new PersonRules(TenantId, Uuid.CreateVersion4());

        // Act
        var stale = person.CorrelateMembership(5, Uuid.CreateVersion4(), Author, Now);
        var missing = absent.CorrelateMembership(1, Uuid.CreateVersion4(), Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(missing).Code);
        Assert.Equal(1, person.Revision);
    }

    [Fact]
    public void ShouldReturnOriginalRegistrationGivenCreationRetriedAfterLaterFacts()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", "ada@example.com", Author, Now).IsSuccess);
        Assert.Null(person.Revise(1, "Ada King", "ada@example.com", Author, Now.AddMinutes(1)));
        Assert.Null(person.CorrelateMembership(2, Uuid.CreateVersion4(), Author, Now.AddMinutes(2)));

        // Act
        var retry = person.Record("Ada Lovelace", "ada@example.com", Author, Now.AddMinutes(3));
        var changed = person.Record("Ada King", "ada@example.com", Author, Now.AddMinutes(3));

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(person.Id, retry.Value.PersonId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Equal(3, person.PendingEvents.Count);
        Assert.Equal(3, person.Revision);
    }

    [Fact]
    public void ShouldRestoreRevisionAndCorrelationGivenValidHistoryReplay()
    {
        // Arrange
        var source = new PersonRules(TenantId, Uuid.CreateVersion4());
        var userId = Uuid.CreateVersion4();
        Assert.True(source.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        Assert.Null(source.Revise(1, "Ada King", "ada@example.com", Author, Now.AddMinutes(1)));
        Assert.Null(source.CorrelateMembership(2, userId, Author, Now.AddMinutes(2)));
        var replayed = new PersonRules(TenantId, source.Id);

        // Act
        foreach (var fact in source.PendingEvents)
            replayed.Observe(fact);
        var stale = replayed.Revise(2, "Stale", null, Author, Now.AddMinutes(3));
        var current = replayed.Revise(3, "Ada Byron", null, Author, Now.AddMinutes(3));

        // Assert
        Assert.True(replayed.IsCreated);
        Assert.Equal((Uuid?)userId, replayed.CorrelatedUserId);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Null(current);
        Assert.Equal(4, replayed.Revision);
        Assert.Equal((Uuid?)userId, replayed.Current!.CorrelatedUserId);
    }

    [Fact]
    public void ShouldKeepCorrelationAndPersonalContactGivenRevisionWithoutContact()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        var contact = new PersonalContactDetails("ada.personal@example.com", "+1 202 555 0147");
        var userId = Uuid.CreateVersion4();
        Assert.True(person.Record("Ada Lovelace", null, contact, Author, Now).IsSuccess);
        Assert.Null(person.CorrelateMembership(1, userId, Author, Now.AddMinutes(1)));

        // Act
        var revised = person.Revise(2, "Ada King", "ada@example.com", Author, Now.AddMinutes(2));

        // Assert
        Assert.Null(revised);
        Assert.Equal(3, person.Revision);
        Assert.Equal(contact, person.Current!.PersonalContact);
        Assert.Equal((Uuid?)userId, person.Current.CorrelatedUserId);
        Assert.Equal("Ada King", person.Current.DisplayName);
    }

    [Fact]
    public void ShouldReplaceCorrelationGivenDifferentUserAtCurrentRevision()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        Assert.Null(person.CorrelateMembership(1, first, Author, Now));

        // Act
        var replaced = person.CorrelateMembership(2, second, Author, Now.AddMinutes(1));

        // Assert
        Assert.Null(replaced);
        Assert.Equal((Uuid?)second, person.CorrelatedUserId);
        Assert.Equal(3, person.Revision);
        Assert.Equal([first, second],
            person.PendingEvents.OfType<PersonMembershipCorrelated>().Select(ev => ev.UserId!.Value));
    }

    [Fact]
    public void ShouldAppendNothingGivenClearOfNeverCorrelatedPerson()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);

        // Act
        var cleared = person.CorrelateMembership(1, null, Author, Now.AddMinutes(1));

        // Assert
        Assert.Null(cleared);
        Assert.Equal(1, person.Revision);
        Assert.Single(person.PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenEmptyCorrelationOrInvalidRevision()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);

        // Act
        var empty = person.CorrelateMembership(1, Uuid.Empty, Author, Now.AddMinutes(1));
        var invalid = person.Revise(1, " ", null, Author, Now.AddMinutes(1));
        var badEmail = person.Revise(1, "Ada", "no-at-sign", Author, Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(empty).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(invalid).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(badEmail).Code);
        Assert.Equal(1, person.Revision);
        Assert.Single(person.PendingEvents);
    }

    [Fact]
    public void ShouldNormalizeBlankContactAndAcceptBoundaryNameGivenRecordRetry()
    {
        // Arrange
        var person = new PersonRules(TenantId, Uuid.CreateVersion4());
        var name = new string('a', 200);

        // Act
        var recorded = person.Record(name, "  ", new PersonalContactDetails(" ", null), Author, Now);
        var retry = person.Record(name, null, Author, Now.AddMinutes(1));

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.True(retry.IsSuccess);
        var fact = Assert.IsType<PersonRecorded>(Assert.Single(person.PendingEvents));
        Assert.Equal(name, fact.DisplayName);
        Assert.Null(fact.WorkEmail);
        Assert.Null(fact.PersonalContact);
    }
}
