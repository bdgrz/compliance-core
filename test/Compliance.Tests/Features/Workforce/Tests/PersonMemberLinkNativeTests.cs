using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class PersonMemberLinkNativeTests
{
    [Fact]
    public async Task ShouldRetainManyAdmittedMembersGivenNativePersonAppendAndReconstruction()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var first = await AdmissionAsync(fixture);
        var second = await AdmissionAsync(fixture);

        // Act
        Assert.True((await LinkAsync(fixture, first)).IsSuccess);
        Assert.True((await LinkAsync(fixture, second)).IsSuccess);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(fixture.TenantId, fixture.PersonId);

        // Assert
        Assert.Equal(3, snapshot.View!.Revision);
        Assert.Equal(2, snapshot.View.MemberLinks!.Count);
        Assert.Contains(snapshot.View.MemberLinks, value => value.MemberId == first.Facts.MemberId);
        Assert.Contains(snapshot.View.MemberLinks, value => value.MemberId == second.Facts.MemberId);
        Assert.Equal("Ada", snapshot.View.DisplayName);
    }

    [Fact]
    public async Task ShouldReturnOriginalWithoutNewFactGivenAdmittedLinkReplayed()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var admission = await AdmissionAsync(fixture);
        Assert.True((await LinkAsync(fixture, admission)).IsSuccess);
        var before = await PersonFactsAsync(fixture);

        // Act
        var result = await LinkAsync(fixture, admission);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before, await PersonFactsAsync(fixture));
    }

    [Fact]
    public async Task ShouldRetainOtherLinkGivenOneAdmittedMemberUnlinked()
    {
        // Arrange
        await using var fixture = await AttestWorkforceManagementWallTests.Fixture.CreateAsync();
        var first = await AdmissionAsync(fixture);
        var second = await AdmissionAsync(fixture);
        Assert.True((await LinkAsync(fixture, first)).IsSuccess);
        Assert.True((await LinkAsync(fixture, second)).IsSuccess);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var operations = scope.ServiceProvider.GetRequiredService<PersonOperations>();
        var before = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(fixture.TenantId, fixture.PersonId);
        var link = before.View!.MemberLinks!.Single(value => value.MemberId == first.Facts.MemberId);
        var request = new UnlinkPersonFromMember(Uuid.CreateVersion4(), fixture.TenantId, fixture.PersonId, first.Facts.UserId);

        // Act
        var result = await operations.UnlinkMemberAsync(Context(fixture, request), link, CancellationToken.None);
        var actual = await scope.ServiceProvider.GetRequiredService<PersonReader>().ReadAsync(fixture.TenantId, fixture.PersonId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(second.Facts.MemberId, Assert.Single(actual.View!.MemberLinks!).MemberId);
        Assert.IsType<PersonUnlinkedFromMember>((await PersonFactsAsync(fixture))[^1]);
    }

    static async Task<VerifiedSourceProof<MemberPersonLinkAdmitted>> AdmissionAsync(AttestWorkforceManagementWallTests.Fixture fixture)
    {
        var user = Uuid.CreateVersion4();
        var member = new Member(fixture.TenantId, user);
        await ProgramManagementServices.SeedAsync(fixture.Provider, member, owner => owner.Register());
        var command = Uuid.CreateVersion4();
        var request = new LinkPersonToMember(command, fixture.TenantId, fixture.PersonId, user);
        var operation = RootOperation.Provenance(command, nameof(LinkPersonToMember), member.Stream,
            ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Client author"), null,
            AuthoredIntentDigest.Compute(request), DateTimeOffset.UtcNow);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.TenantId, user, selectedCommandId: command),
            owner => owner.RequestPersonLink(operation, NativeFacts.Ref(), fixture.PersonId, 0));
        await using var scope = fixture.Provider.CreateAsyncScope();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(member.Stream, 0))
        {
            if (record.Event is MemberPersonLinkAdmitted admitted)
                return new VerifiedSourceProof<MemberPersonLinkAdmitted>(member.Stream, admitted.Metadata.EventId,
                    record.ResourceOffset, admitted.Metadata.AggregateVersion, ContentDigest.OfJson(admitted), admitted, 1, 1);
        }
        throw new InvalidOperationException("The admitted Member source was not committed.");
    }

    static async Task<Result<UnitResult>> LinkAsync(AttestWorkforceManagementWallTests.Fixture fixture,
        VerifiedSourceProof<MemberPersonLinkAdmitted> admission)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var request = new LinkPersonToMember(admission.Facts.LinkId, fixture.TenantId, fixture.PersonId, admission.Facts.UserId);
        return await scope.ServiceProvider.GetRequiredService<PersonOperations>().LinkMemberAsync(Context(fixture, request),
            admission, CancellationToken.None);
    }

    static RequestContext<T> Context<T>(AttestWorkforceManagementWallTests.Fixture fixture, T request) where T : IRequestBase =>
        new(request, new RequestDispatchContext(ProgramManagementServices.Actor(fixture.UserId),
            new HttpInvocation("PUT", "/synthetic/person/members", "/synthetic/person/members", "synthetic")));

    static async Task<IReadOnlyList<DomainEvent>> PersonFactsAsync(AttestWorkforceManagementWallTests.Fixture fixture)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var facts = new List<DomainEvent>();
        await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                           .ReadAsync(new Person(fixture.TenantId, fixture.PersonId).Stream, 0))
            facts.Add(record.Event);
        return facts;
    }
}
