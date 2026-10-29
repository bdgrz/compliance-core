using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Tests;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class ArtifactDownloadGrantTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly Uuid CommandId = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Member");
    static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldDeriveGrantIdentityGivenArtifactTenantAndCommand()
    {
        // Arrange
        var expected = Uuid.CreateVersion5(ArtifactId,
            $"artifact-download-grant:v1:{TenantId}:{CommandId}");

        // Act
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);

        // Assert
        Assert.Equal(expected, grant.Id);
        Assert.Equal(TenantId.ToString(), grant.Stream.Realm);
        Assert.Equal(ArtifactDownloadGrant.Area, grant.Stream.Area);
    }

    [Fact]
    public void ShouldFenceIssueAfterCancellationGivenOneTerminalGrantOutcome()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);
        Assert.True(grant.Cancel(1, operation, Ref("intent"), Ref("source-proofs"), Ref("result")).IsSuccess);

        // Act
        var issued = grant.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now,
            operation, Ref("intent"), Ref("ticket"));

        // Assert
        Assert.False(issued.IsSuccess);
        Assert.True(grant.IsCanceled);
        Assert.False(grant.IsIssued);
        Assert.Single(new AggregateScenario<ArtifactDownloadGrant>(grant).PendingEvents);
    }

    [Fact]
    public void ShouldTreatCancellationAsNoOpGivenIssuedGrantTerminalOutcome()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);
        Assert.True(grant.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now,
            operation, Ref("intent"), Ref("ticket")).IsSuccess);

        // Act
        var canceled = grant.Cancel(1, operation, Ref("intent"), Ref("source-proofs"), Ref("result"));

        // Assert
        Assert.True(canceled.IsSuccess);
        Assert.True(grant.IsIssued);
        Assert.False(grant.IsCanceled);
        Assert.Single(new AggregateScenario<ArtifactDownloadGrant>(grant).PendingEvents);
    }

    [Fact]
    public void ShouldReplayOriginalTicketGivenExactIssueRetryAndRefuseChangedIntent()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);
        var intent = Ref("intent");
        var result = Ref("ticket");
        Assert.True(grant.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now,
            operation, intent, result).IsSuccess);

        // Act
        var retry = grant.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now,
            operation, intent, result);
        var changed = grant.Issue(CommandId, new string('a', 64), 10, "Different purpose", Actor, Now,
            operation, intent, result);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Same(result, grant.Issued!.OperationResult);
        Assert.Single(new AggregateScenario<ArtifactDownloadGrant>(grant).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenInvalidIssueOrCancellation()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);

        // Act
        var wrongRequest = grant.Issue(Uuid.CreateVersion4(), new string('a', 64), 10, "Review evidence", Actor,
            Now, operation, Ref("intent"), Ref("ticket"));
        var badHash = grant.Issue(CommandId, new string('A', 64), 10, "Review evidence", Actor, Now,
            operation, Ref("intent"), Ref("ticket"));
        var emptyLength = grant.Issue(CommandId, new string('a', 64), 0, "Review evidence", Actor, Now,
            operation, Ref("intent"), Ref("ticket"));
        var badGeneration = grant.Cancel(0, operation, Ref("intent"), Ref("source-proofs"), Ref("result"));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, wrongRequest.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, badHash.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, emptyLength.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, badGeneration.Error?.Kind);
        Assert.Empty(new AggregateScenario<ArtifactDownloadGrant>(grant).PendingEvents);
        Assert.False(grant.IsIssued);
        Assert.False(grant.IsCanceled);
    }

    [Fact]
    public void ShouldReplayCancellationGivenExactRetryAndRefuseChangedGeneration()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);
        var (intent, proofs, result) = (Ref("intent"), Ref("source-proofs"), Ref("result"));
        Assert.True(grant.Cancel(1, operation, intent, proofs, result).IsSuccess);

        // Act
        var retry = grant.Cancel(1, operation, intent, proofs, result);
        var changed = grant.Cancel(2, operation, intent, proofs, result);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Single(new AggregateScenario<ArtifactDownloadGrant>(grant).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreIssuedTicketAndAcceptExactRetryGivenValidHistoryReplay()
    {
        // Arrange
        var source = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(source);
        var (intent, ticket) = (Ref("intent"), Ref("ticket"));
        Assert.True(source.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now, operation,
            intent, ticket).IsSuccess);
        var history = NativeFacts.Raised(source);
        var replayed = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        new AggregateScenario<ArtifactDownloadGrant>(replayed).Given(history);

        // Act
        var retry = replayed.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now, operation,
            intent, ticket);
        var cancel = replayed.Cancel(1, operation, intent, Ref("source-proofs"), Ref("result"));

        // Assert
        Assert.True(replayed.IsIssued);
        Assert.False(replayed.IsCanceled);
        Assert.True(retry.IsSuccess);
        Assert.True(cancel.IsSuccess);
        Assert.Equal(ticket, replayed.Issued!.OperationResult);
    }

    [Fact]
    public void ShouldRestoreCancellationAndFenceIssueGivenValidHistoryReplay()
    {
        // Arrange
        var source = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(source);
        Assert.True(source.Cancel(1, operation, Ref("intent"), Ref("source-proofs"), Ref("result")).IsSuccess);
        var history = NativeFacts.Raised(source);
        var replayed = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        new AggregateScenario<ArtifactDownloadGrant>(replayed).Given(history);

        // Act
        var issue = replayed.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now, operation,
            Ref("intent"), Ref("ticket"));

        // Assert
        Assert.True(replayed.IsCanceled);
        Assert.False(replayed.IsIssued);
        Assert.Equal(RequestErrorKind.Conflict, issue.Error?.Kind);
    }

    static OperationProvenance Operation(ArtifactDownloadGrant grant) => new(CommandId, CommandId, null,
        "IssueArtifactDownload", Actor, grant.Stream, "initial", null, new string('b', 64), Now,
        grant.Stream, grant.Stream);

    static ImmutableContentRef Ref(string schema) => new(Uuid.CreateVersion4(), new string('c', 64), 1,
        "application/json", schema);
}
