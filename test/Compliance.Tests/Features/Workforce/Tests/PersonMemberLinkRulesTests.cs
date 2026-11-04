using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonMemberLinkRulesTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Id = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");

    [Fact]
    public void ShouldRetainBothMembersGivenIndependentAdmittedLinks()
    {
        // Arrange
        var person = Recorded();
        var first = Link();
        var second = Link();

        // Act
        var firstResult = person.LinkMember(first, Actor, Now);
        var secondResult = person.LinkMember(second, Actor, Now);

        // Assert
        Assert.Null(firstResult);
        Assert.Null(secondResult);
        Assert.Equal(new[] { first, second }.OrderBy(value => value.MemberId.ToString(), StringComparer.Ordinal), person.Current!.MemberLinks);
        Assert.Equal(3, person.Revision);
    }

    [Fact]
    public void ShouldPreserveOtherLinksGivenOneMemberUnlinked()
    {
        // Arrange
        var person = Recorded();
        var first = Link();
        var second = Link();
        Assert.Null(person.LinkMember(first, Actor, Now));
        Assert.Null(person.LinkMember(second, Actor, Now));

        // Act
        var result = person.UnlinkMember(first, Actor, Now);

        // Assert
        Assert.Null(result);
        Assert.Equal(second, Assert.Single(person.Current!.MemberLinks!));
        Assert.Equal(4, person.Revision);
    }

    [Fact]
    public void ShouldLeaveRevisionUnchangedGivenExactLinkRestated()
    {
        // Arrange
        var person = Recorded();
        var link = Link();
        Assert.Null(person.LinkMember(link, Actor, Now));

        // Act
        var result = person.LinkMember(link, Actor, Now.AddDays(1));

        // Assert
        Assert.Null(result);
        Assert.Equal(2, person.Revision);
        Assert.Equal(Now, person.Current!.LastChangedAt);
    }

    [Fact]
    public void ShouldRefuseWithoutMutationGivenDifferentAdmissionForLinkedMember()
    {
        // Arrange
        var person = Recorded();
        var link = Link();
        Assert.Null(person.LinkMember(link, Actor, Now));
        var before = person.Current;

        // Act
        var result = person.LinkMember(link with { LinkId = Uuid.CreateVersion4() }, Actor, Now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(before, person.Current);
    }

    [Fact]
    public void ShouldRefuseWithoutMutationGivenUnlinkFromOlderGeneration()
    {
        // Arrange
        var person = Recorded();
        var link = Link();
        Assert.Null(person.LinkMember(link, Actor, Now));
        var before = person.Current;

        // Act
        var result = person.UnlinkMember(link with { Generation = link.Generation + 1 }, Actor, Now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(before, person.Current);
    }

    static PersonRules Recorded()
    {
        var person = new PersonRules(Tenant, Id);
        Assert.True(person.Record("Person", "person@example.com", Actor, Now).IsSuccess);
        return person;
    }

    static PersonMemberLink Link() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Uuid.CreateVersion4());
}
