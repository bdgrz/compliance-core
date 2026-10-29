using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
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
    public void ShouldRefuseCancellationAfterIssueGivenOneTerminalGrantOutcome()
    {
        // Arrange
        var grant = new ArtifactDownloadGrant(TenantId, ArtifactId, CommandId);
        var operation = Operation(grant);
        Assert.True(grant.Issue(CommandId, new string('a', 64), 10, "Review evidence", Actor, Now,
            operation, Ref("intent"), Ref("ticket")).IsSuccess);

        // Act
        var canceled = grant.Cancel(1, operation, Ref("intent"), Ref("source-proofs"), Ref("result"));

        // Assert
        Assert.False(canceled.IsSuccess);
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

    static OperationProvenance Operation(ArtifactDownloadGrant grant) => new(CommandId, CommandId, null,
        "IssueArtifactDownload", Actor, grant.Stream, "initial", null, new string('b', 64), Now,
        grant.Stream, grant.Stream);

    static ImmutableContentRef Ref(string schema) => new(Uuid.CreateVersion4(), new string('c', 64), 1,
        "application/json", schema);
}
