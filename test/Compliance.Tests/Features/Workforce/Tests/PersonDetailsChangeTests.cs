using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonDetailsChangeTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Person = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("personal_contact")]
    public void ShouldChangeOnlyRequestedDetailsGivenDifferentCanonicalValues(string field)
    {
        // Arrange
        var rules = Recorded();
        var name = field == "name" ? " Grace Hopper " : "Ada";
        var email = field == "email" ? " grace@example.com " : "ada@example.com";
        var contact = field == "personal_contact" ? new PersonalContactDetails(null, " 555-0100 ") : null;

        // Act
        var failure = rules.ChangeDetails(1, name, email, contact, Author, Now.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, rules.Revision);
        Assert.Equal(name.Trim(), rules.Current!.DisplayName);
        Assert.Equal(email.Trim(), rules.Current.WorkEmail);
        Assert.Equal(field == "personal_contact" ? new PersonalContactDetails(null, "555-0100") : null,
            rules.Current.PersonalContact);
        Assert.Equal(Author, rules.Current.LastChangedBy);
        Assert.Equal(Now.AddMinutes(1), rules.Current.LastChangedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRefuseWithoutFactGivenCanonicalDetailsAlreadyMatch(bool whitespace)
    {
        // Arrange
        var rules = Recorded();
        var before = rules.PendingEvents.ToArray();

        // Act
        var failure = rules.ChangeDetails(1, whitespace ? " Ada " : "Ada", "ada@example.com",
            whitespace ? new PersonalContactDetails(" ", " ") : null, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(1, rules.Revision);
        Assert.Equal(before, rules.PendingEvents);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("name")]
    [InlineData("email")]
    public void ShouldRefuseWithoutFactGivenInvalidNamedChange(string invalid)
    {
        // Arrange
        var rules = invalid == "missing" ? new PersonRules(Tenant, Person) : Recorded();
        var before = rules.PendingEvents.ToArray();

        // Act
        var failure = rules.ChangeDetails(invalid == "stale" ? 9 : 1,
            invalid == "name" ? " " : "Grace", invalid == "email" ? "invalid" : "grace@example.com",
            null, Author, Now);

        // Assert
        Assert.Equal(invalid == "missing" ? CommandFailureCode.MissingRecord :
            invalid == "stale" ? CommandFailureCode.VersionConflict : CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(before, rules.PendingEvents);
    }

    [Fact]
    public void ShouldPreserveCurrentMemberLinkGivenDetailsChange()
    {
        // Arrange
        var rules = Recorded();
        var user = Uuid.CreateVersion4();
        Assert.Null(rules.CorrelateMembership(1, user, Author, Now));

        // Act
        var failure = rules.ChangeDetails(2, "Grace", "grace@example.com", null, Author, Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(user, rules.CorrelatedUserId);
        Assert.Equal(3, rules.Revision);
    }

    static PersonRules Recorded()
    {
        var rules = new PersonRules(Tenant, Person);
        Assert.True(rules.Record("Ada", "ada@example.com", Author, Now).IsSuccess);
        return rules;
    }
}
