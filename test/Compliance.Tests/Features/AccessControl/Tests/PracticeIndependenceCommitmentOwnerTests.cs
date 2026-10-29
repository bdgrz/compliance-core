using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PracticeIndependenceCommitmentOwnerTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("actor")]
    [InlineData("episode")]
    [InlineData("expected")]
    [InlineData("digest")]
    [InlineData("affected")]
    [InlineData("revision_owner")]
    public async Task ShouldCommitOnlyMatchingRootGivenFinalizedReservation(string variant)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var root = Uuid.CreateVersion4();
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        var firstOwner = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var foreign = new ServiceEngagementIndependence(host.Tenant, Uuid.CreateVersion4()).Stream;
        var key = new ReservationKey(root, firstOwner, 1, stream, "actual_staff_assignment");
        var reservationIntent = NativeFacts.Ref(SourceUseAuthorizationIntent.Schema);
        var commitmentIntent = NativeFacts.Ref();
        var reserve = new OperationProvenance(Uuid.CreateVersion4(), root, null,
            nameof(ReservePracticeIndependence), host.MemberReference, stream, "initial", 1, new string('a', 64),
            IndependenceHost.At, stream, stream, firstOwner);
        var executor = host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var held = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant,
                PracticeSubjectKind.Staff, subject, reserve.LogicalId, "attest", engagement).ForSourceUses(key),
            owner => AggregateOutcome.Commit(owner.Reserve(reserve, reservationIntent, 1)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(held.IsSuccess, held.Error?.Message);
        var commit = reserve with { LogicalId = Uuid.CreateVersion4(), CommandKind = nameof(CommitPracticeIndependence) };
        commit = variant switch
        {
            "valid" => commit,
            "actor" => commit with { Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Other") },
            "episode" => commit with { Episode = "another" },
            "expected" => commit with { OriginalExpectedRevision = 2 },
            "digest" => commit with { AuthoredIntentSha256 = new string('b', 64) },
            "affected" => commit with { AffectedOwner = foreign },
            "revision_owner" => commit with { ExpectedRevisionOwner = foreign },
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };

        // Act
        var result = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant,
                PracticeSubjectKind.Staff, subject, commit.LogicalId).ForSourceUses(key),
            owner => AggregateOutcome.Commit(owner.Commit(commit, commitmentIntent, 1, "attest", engagement)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        var facts = await host.EventsAsync<PracticeIndependenceCommitted>(stream);
        if (variant == "valid")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Single(facts);
        }
        else
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
            Assert.Empty(facts);
        }
    }
}
