using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

/// <summary>Owner-level tests of the import batch: staging bounds, valid replay, and publication intent.</summary>
public sealed class ApplicationImportBatchOwnerTests
{
    [Fact]
    public void ShouldStageAtRevisionOneGivenFreshBatch()
    {
        // Arrange
        var submission = Uuid.CreateVersion4();
        var owner = new ApplicationImportBatch(Uuid.CreateVersion4(), Uuid.CreateVersion4(), submission);

        // Act
        var result = Stage(owner, submission, 3, 1);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var staged = Assert.IsType<ApplicationImportStaged>(
            Assert.Single(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents));
        Assert.Equal(submission, staged.SubmissionId);
        Assert.Equal(3, staged.RowCount);
        Assert.Equal(1, owner.Revision);
        Assert.True(owner.IsCreated);
        Assert.Equal("preview_ready", owner.State);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(2, 3)]
    public void ShouldAppendNothingGivenInvalidRowCounts(int rowCount, int invalidCount)
    {
        // Arrange
        var submission = Uuid.CreateVersion4();
        var owner = new ApplicationImportBatch(Uuid.CreateVersion4(), Uuid.CreateVersion4(), submission);

        // Act
        var result = Stage(owner, submission, rowCount, invalidCount);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents);
        Assert.False(owner.IsCreated);
        Assert.Equal(0, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenStagingRootedUnderAnotherSubmission()
    {
        // Arrange
        var owner = new ApplicationImportBatch(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "StageApplicationImport", null);

        // Act
        var result = owner.Stage(operation, Uuid.CreateVersion4(), "manual", "applications", "partial",
            new string('c', 64), NativeFacts.Ref(), 1, 0, Uuid.CreateVersion4(), "Tester", NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAppendNothingGivenSecondStagingOfStagedBatch()
    {
        // Arrange
        var submission = Uuid.CreateVersion4();
        var owner = new ApplicationImportBatch(Uuid.CreateVersion4(), Uuid.CreateVersion4(), submission);
        Assert.True(Stage(owner, submission, 1, 0).IsSuccess);

        // Act
        var again = Stage(owner, submission, 1, 0);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Single(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreStateAndDecideRetriesGivenValidStagedHistory()
    {
        // Arrange
        var (tenantId, batchId, submission) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var source = new ApplicationImportBatch(tenantId, batchId, submission);
        var operation = NativeFacts.Root(source, submission, "StageApplicationImport", null);
        Assert.True(source.Stage(operation, submission, "manual", "applications", "partial", new string('c', 64),
            NativeFacts.Ref(), 2, 0, Uuid.CreateVersion4(), "Tester", NativeFacts.Now, NativeFacts.Ref(),
            NativeFacts.Ref()).IsSuccess);
        var facts = NativeFacts.Raised(source);

        // Act
        var owner = NativeFacts.Replay(new ApplicationImportBatch(tenantId, batchId, submission), facts);
        var exact = owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation));
        var changed = owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation with
        {
            AuthoredIntentSha256 = new string('d', 64)
        }));
        var restaged = Stage(owner, submission, 2, 0);

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(RequestErrorKind.Conflict, restaged.Error?.Kind);
        Assert.True(owner.IsCreated);
        Assert.Equal(1, owner.Revision);
        Assert.Equal("manual", owner.SourceKey);
        Assert.Equal("preview_ready", owner.State);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task ShouldAppendNothingGivenPlanWithoutRowsOrTargetGroups(long rows, long groups)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var command = Uuid.CreateVersion4();
        var owner = await host.HydrateAsync(host.NewBatch(batch.Id, command));
        await host.BatchHistoryAsync(owner);
        var operation = NativeFacts.Root(owner, command, "AcceptApplicationImport", 1);

        // Act
        var result = owner.SealPlan(operation, new string('e', 64), NativeFacts.Ref(), rows, groups, NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents);
        Assert.Null(owner.Plan);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenPublicationOfAnotherIntentThanTheSealedPlan()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(batch.Id, 2)).IsSuccess);
        var command = Uuid.CreateVersion4();
        var owner = await host.HydrateAsync(host.NewBatch(batch.Id, command));
        await host.BatchHistoryAsync(owner);
        var authorized = owner.AuthorizeExecution(NativeFacts.Root(owner, command, "ExecuteApplicationImport", null),
            owner.Plan!.FrozenPlan);
        Assert.True(authorized.IsSuccess, authorized.Error?.Message);

        // Act
        var result = owner.Commit(NativeFacts.Root(owner, command, "CommitApplicationImportBatch", null),
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), 1, NativeFacts.Now, NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Single(new AggregateScenario<ApplicationImportBatch>(owner).PendingEvents);
        Assert.Null(owner.Committed);
        Assert.False(owner.IsEnded);
    }

    [Fact]
    public async Task ShouldRefuseSecondCorrelationGivenCompetingDecisionsAtSameExpectedRevision()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);

        // Act
        var competing = await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new");
        var after = await host.BatchAsync(batch.Id);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, competing.Error?.Kind);
        Assert.Equal(2, after.Revision);
    }

    static Result Stage(ApplicationImportBatch owner, Uuid submission, int rowCount, int invalidCount) =>
        owner.Stage(NativeFacts.Root(owner, submission, "StageApplicationImport", null), submission, "manual",
            "applications", "partial", new string('c', 64), NativeFacts.Ref(), rowCount, invalidCount,
            Uuid.CreateVersion4(), "Tester", NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
}
