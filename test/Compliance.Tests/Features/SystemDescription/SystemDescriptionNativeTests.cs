using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SystemDescription;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.SystemDescription;

public sealed class SystemDescriptionNativeTests
{
    [Fact]
    public async Task ShouldReplayOriginalReviewGivenLaterNarrativeRevisionAndExactAuthorizedRetry()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        var request = new ReviewSystemDescription(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, "approve", "Reviewed exact draft", 1);
        var handler = new ReviewSystemDescriptionHandler(fixture.Operations);
        Assert.True((await handler.HandleAsync(fixture.Context(request, fixture.Reviewer), CancellationToken.None)).IsSuccess);
        var revised = await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 2, "Later title", SystemDescriptionNativeFixture.Body().Sections, "Correct narrative")), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);

        // Act
        var replay = await handler.HandleAsync(fixture.Context(request, fixture.Reviewer), CancellationToken.None);
        var actor = await handler.HandleAsync(fixture.Context(request, fixture.Approver), CancellationToken.None);
        var intent = await handler.HandleAsync(fixture.Context(request with { Comments = "Different authored review" }, fixture.Reviewer), CancellationToken.None);
        var revision = await handler.HandleAsync(fixture.Context(request with { ExpectedRevision = 3 }, fixture.Reviewer), CancellationToken.None);
        var current = await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.False(actor.IsSuccess);
        Assert.False(intent.IsSuccess);
        Assert.False(revision.IsSuccess);
        Assert.Equal("Later title", current.CurrentVersion!.Content.Title);
        Assert.Equal(3, current.Revision);
        Assert.Equal(3, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldRecoverOriginalReviewWithoutDuplicateFactGivenUnknownAppendOutcome()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        var operations = new SystemDescriptionOperations(new NativeOperationExecutor(fixture.Reader, new CommitThenFailExecutor(fixture.Executor), fixture.Content),
            fixture.Descriptions, fixture.Content, fixture.Clock);
        var request = new ReviewSystemDescription(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, "approve", "Exact review", 1);

        // Act
        var result = await new ReviewSystemDescriptionHandler(operations).HandleAsync(fixture.Context(request, fixture.Reviewer), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var facts = await fixture.FactsAsync();
        Assert.Equal(2, facts.Count);
        Assert.All(facts, value => Assert.Equal(new Bdgrz.Compliance.Features.SystemDescription.SystemDescription(fixture.Tenant, fixture.Program, fixture.Description).Stream,
            ((ISystemDescriptionFact)value).Operation.FirstOwner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholeScopedReadGivenMissingOrCorruptCurrentContent(bool unavailable)
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        var failed = new FailedContent(fixture.Content, unavailable);
        var reader = new SystemDescriptionReader(fixture.Reader, fixture.Events, failed);
        var handler = new GetSystemDescriptionHandler(reader, new SystemDescriptionReadConsistency(fixture.Reader));

        // Act
        var result = await handler.HandleAsync(fixture.Context(new GetSystemDescription(fixture.Tenant, fixture.Program, fixture.Description)), CancellationToken.None);
        var foreign = await handler.HandleAsync(fixture.Context(new GetSystemDescription(fixture.Tenant, Uuid.CreateVersion4(), fixture.Description)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.Equal(1, failed.Reads);
        Assert.Single(await fixture.FactsAsync());
    }

    [Fact]
    public async Task ShouldFenceUnqualifiedPersistedSourceHistoryGivenApprovalRequiresExactSnapshotProof()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        var body = SystemDescriptionNativeFixture.Body() with
        {
            Sources = [new SystemDescriptionSourceReference("control",Uuid.CreateVersion4(),
            Uuid.CreateVersion4(),1,new string('a',64),"current")]
        };
        await fixture.SeedAsync(content: body);
        var review = await new ReviewSystemDescriptionHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviewSystemDescription(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Description, 1, "approve", "Reviewed exact source", 1), fixture.Reviewer), CancellationToken.None);
        Assert.True(review.IsSuccess, review.Error?.Message);

        // Act
        var result = await new ApproveSystemDescriptionHandler(fixture.Operations).HandleAsync(fixture.Context(new ApproveSystemDescription(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Description, 1, "Approve", 2), fixture.Approver), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(2, (await fixture.FactsAsync()).Count);
        Assert.Equal("draft", (await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description)).CurrentVersion!.Status);
    }

    [Fact]
    public async Task ShouldBindVersionCursorToScopeAndSourceCutGivenConcurrentDescriptionChange()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        Assert.True((await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Description, 1, "Version two", SystemDescriptionNativeFixture.Body().Sections, "Correction")), CancellationToken.None)).IsSuccess);
        var handler = new ListSystemDescriptionVersionsHandler(fixture.Descriptions, new SystemDescriptionReadConsistency(fixture.Reader));
        var first = await handler.HandleAsync(fixture.Context(new ListSystemDescriptionVersions(fixture.Tenant, fixture.Program, fixture.Description, 1)), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.NotNull(first.Value.NextCursor);
        var cursor = first.Value.NextCursor;

        // Act
        var foreign = await handler.HandleAsync(fixture.Context(new ListSystemDescriptionVersions(fixture.Tenant, Uuid.CreateVersion4(), fixture.Description, 1, cursor)), CancellationToken.None);
        Assert.True((await new ReviewSystemDescriptionHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviewSystemDescription(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Description, 2, "approve", "Reviewed version two", 2), fixture.Reviewer), CancellationToken.None)).IsSuccess);
        var changed = await handler.HandleAsync(fixture.Context(new ListSystemDescriptionVersions(fixture.Tenant, fixture.Program, fixture.Description, 1, cursor)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, foreign.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.True(changed.Error.IsTransient);
        Assert.Equal("superseded", Assert.Single(first.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldEditCurrentDraftWithoutLoadingHistoricalNarrativeGivenOlderSourceBodiesUnavailable()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        await fixture.SeedAsync();
        var original = Assert.IsType<SystemDescriptionDraftCreated>(Assert.Single(await fixture.FactsAsync()));
        Assert.True((await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, "Current second version", SystemDescriptionNativeFixture.Body().Sections, "First revision")), CancellationToken.None)).IsSuccess);
        var unavailable = new HistoricalUnavailableContent(fixture.Content, [original.CurrentVersionContent.ContentId,original.VersionContent.ContentId,
            original.CurrentAuthorityContent.ContentId]);
        var reader = new SystemDescriptionReader(fixture.Reader, fixture.Events, unavailable);
        var operations = new SystemDescriptionOperations(new NativeOperationExecutor(fixture.Reader, fixture.Executor, unavailable), reader, unavailable, fixture.Clock);

        // Act
        var revised = await new ReviseSystemDescriptionDraftHandler(operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 2, "Current third version", SystemDescriptionNativeFixture.Body().Sections, "Second revision")), CancellationToken.None);

        // Assert
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        Assert.Equal(0, unavailable.BlockedReads);
        var current = await fixture.Descriptions.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description);
        Assert.Equal(3, current.View.Versions.Count);
        Assert.Equal("Current third version", current.CurrentVersion!.Content.Title);
        await Assert.ThrowsAsync<ImmutableContentUnavailableException>(async () => await reader.ReadAsync(fixture.Tenant, fixture.Program, fixture.Description));
        Assert.Equal(1, unavailable.BlockedReads);
    }

    [Fact]
    public async Task ShouldRejectHistoricalVersionIdentityReuseGivenCurrentPreparationOmitsOldNarrative()
    {
        // Arrange
        await using var fixture = new SystemDescriptionNativeFixture();
        var initial = await fixture.SeedAsync();
        var versionId = initial.CurrentVersion!.SystemDescriptionVersionId;
        Assert.True((await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(
            Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Description, 1, "Current second version", SystemDescriptionNativeFixture.Body().Sections, "First revision")), CancellationToken.None)).IsSuccess);

        // Act
        var result = await new ReviseSystemDescriptionDraftHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseSystemDescriptionDraft(
            versionId, fixture.Tenant, fixture.Program, fixture.Description, 2, "Reused identity", SystemDescriptionNativeFixture.Body().Sections, "Must refuse")), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.False(result.Error.IsTransient);
        Assert.Equal(2, (await fixture.FactsAsync()).Count);
    }

    sealed class HistoricalUnavailableContent(IImmutableContentStore inner, IReadOnlyList<Uuid> blocked) : IImmutableContentStore
    {
        public int BlockedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (blocked.Contains(reference.ContentId))
            { BlockedReads++; throw new ImmutableContentUnavailableException("Historical source intentionally unavailable"); }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class FailedContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == SystemDescriptionContent.CurrentSchema)
            { Reads++; throw unavailable ? new ImmutableContentUnavailableException("Absent source body") : new ImmutableContentIntegrityException("Corrupt source body"); }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
    sealed class CommitThenFailExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;
        public ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> action, IExecutionContext context, CancellationToken ct) where T : Aggregate =>
            inner.ExecuteAsync(aggregate, action, context, ct);
        public async ValueTask<Result<TOut>> ExecuteAsync<T, TOut>(T aggregate, Func<T, AggregateOutcome<TOut>> action, IExecutionContext context, CancellationToken ct) where T : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, action, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Unknown append outcome", new TimeoutException());
            return result;
        }
    }
}
