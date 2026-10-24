using System.Security.Claims;
using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftHistoryReadTests
{
    [Fact]
    public async Task ShouldPreserveImmutableHistoryAndPageByDraftGivenCommittedReplay()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(native.Kv, native.Commitments);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-01");
        var otherDraftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02");
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await ProjectChangeAsync(native, directory, Created(tenantId, programId, draftId, actorId,
                "First statement", now));
            await ProjectChangeAsync(native, directory, Revised(tenantId, programId, draftId, 2, actorId,
                "Second statement", now.AddMinutes(1)));
            await ProjectChangeAsync(native, directory, Revised(tenantId, programId, draftId, 3, actorId,
                "Third statement", now.AddMinutes(2)));
            await ProjectChangeAsync(native, directory, Created(tenantId, programId, otherDraftId, actorId,
                "Other first statement", now));
            await ProjectChangeAsync(native, directory, Revised(tenantId, programId, otherDraftId, 2, actorId,
                "Other second statement", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var first = await directory.ListRevisionsAsync(tenantId, draftId, 2, null);
        var second = await directory.ListRevisionsAsync(tenantId, draftId, 2, first.NextCursor);
        var original = await directory.GetRevisionAsync(tenantId, draftId, 1);

        // Assert
        Assert.Equal([1L, 2L], first.Items.Select(item => item.Revision));
        Assert.Equal([3L], second.Items.Select(item => item.Revision));
        Assert.Equal("First statement", original?.Statement);
        Assert.Equal("Source A", original?.SourceReference);
        var latest = Assert.Single(second.Items);
        Assert.Equal("Third statement", latest.Statement);
        Assert.Equal("Source C", latest.SourceReference);
        Assert.Equal(first.Items[0].ServiceId, latest.ServiceId);
        Assert.Equal(first.Items[0].Kind, latest.Kind);
        Assert.Equal(first.Items[0].Identifier, latest.Identifier);
    }

    [Fact]
    public async Task ShouldRejectMalformedAndCrossRecordCursorsGivenHistoryPages()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(native.Kv, native.Commitments);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-01");
        var otherDraftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02");
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Checkpoint(tenantId), ProjectionCheckpoint.Start)))
        {
            await ProjectChangeAsync(native, directory, Created(tenantId, programId, draftId, actorId,
                "First statement", now));
            await ProjectChangeAsync(native, directory, Revised(tenantId, programId, draftId, 2, actorId,
                "Second statement", now.AddMinutes(1)));
            await ProjectChangeAsync(native, directory, Created(tenantId, programId, otherDraftId, actorId,
                "Other first statement", now));
            await ProjectChangeAsync(native, directory, Revised(tenantId, programId, otherDraftId, 2, actorId,
                "Other second statement", now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var first = await directory.ListRevisionsAsync(tenantId, draftId, 1, null);
        var otherFirst = await directory.ListRevisionsAsync(tenantId, otherDraftId, 1, null);

        // Act
        var malformed = await Record.ExceptionAsync(async () =>
            await directory.ListRevisionsAsync(tenantId, draftId, 1, "not-a-cursor"));
        var crossRecord = await Record.ExceptionAsync(async () =>
            await directory.ListRevisionsAsync(tenantId, draftId, 1, otherFirst.NextCursor));
        var crossTenant = await Record.ExceptionAsync(async () =>
            await directory.ListRevisionsAsync(otherTenantId, draftId, 1, first.NextCursor));

        // Assert
        Assert.IsType<KvDirectoryQueryException>(malformed);
        Assert.IsType<KvDirectoryQueryException>(crossRecord);
        Assert.IsType<KvDirectoryQueryException>(crossTenant);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenDraftHistoryRequest(int limit)
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync();
        var handler = Handler(scenario);

        // Act
        var result = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, scenario.ProgramId, scenario.DraftId, Limit: limit)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldMapMalformedOrCrossRecordCursorToValidationGivenDraftHistoryRequest()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(rejectCursor: true);
        var handler = Handler(scenario);

        // Act
        var malformed = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, scenario.ProgramId, scenario.DraftId, Cursor: "not-a-cursor")),
            CancellationToken.None);
        var crossRecord = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, scenario.ProgramId, scenario.DraftId, Cursor: "other-record")),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(malformed.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(crossRecord.Error).Kind);
    }

    [Fact]
    public async Task ShouldReportHistoryLagGivenSourceRevisionMissingFromV1Projection()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(sourceRevision: 2, projectedRevision: null);
        var handler = Handler(scenario);

        // Act
        var result = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, scenario.ProgramId, scenario.DraftId,
            MinimumDraftRevision: 2)), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Contains("projection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReportSourceLagGivenRequestedHistoryRevisionIsAhead()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(sourceRevision: 1, projectedRevision: 1);
        var handler = Handler(scenario);

        // Act
        var result = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, scenario.ProgramId, scenario.DraftId,
            MinimumDraftRevision: 2)), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
        Assert.Contains("source", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotDiscloseHistoryGivenDraftBelongsToAnotherProgram()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync();
        var handler = Handler(scenario);

        // Act
        var result = await handler.HandleAsync(Context(new ListCommitmentDraftRevisions(
            scenario.TenantId, Uuid.CreateVersion4(), scenario.DraftId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(scenario.Directory.ListWasCalled);
    }

    static CheckpointIdentity Checkpoint(Uuid tenantId) => new(
        FitzCommitmentDraftHistoryDirectoryV1.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts"));

    static CommitmentDraftCreatedContent Created(Uuid tenantId, Uuid programId, Uuid draftId,
        Uuid actorId, string statement, DateTimeOffset changedAt) => new(tenantId, programId,
        draftId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", draftId == CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-01") ? "SC-01" : "SC-02",
        statement, "Context", "Source A", actorId, "Author", changedAt);

    static CommitmentDraftRevisedContent Revised(Uuid tenantId, Uuid programId, Uuid draftId,
        long revision, Uuid actorId, string statement, DateTimeOffset changedAt) => new(tenantId,
        programId, draftId, revision, statement, "Updated context",
        revision == 3 ? "Source C" : "Source B", actorId, "Editor", changedAt);

    static async Task<Scenario> CreateScenarioAsync(long sourceRevision = 1, long? projectedRevision = 1,
        bool rejectCursor = false)
    {
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var draftId = CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-01");
        var source = new CommitmentRules(tenantId, draftId);
        var native = new CommitmentProjectionScenario();
        var actorId = Uuid.CreateVersion4();
        Assert.True(source.Create(programId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "service_commitment", "SC-01", "First statement", "Context", "Source A",
            actorId, "Author", DateTimeOffset.UtcNow).IsSuccess);
        while (source.Revision < sourceRevision)
        {
            Assert.Null(source.Revise(programId, source.Revision, "Updated statement", "Context",
                "Source B", actorId, "Editor", DateTimeOffset.UtcNow));
        }
        var historyRevision = projectedRevision is { } value
            ? Revision(tenantId, programId, draftId, value)
            : null;
        await native.PublishAsync(source);
        return new Scenario(tenantId, programId, draftId, native, new HistoryDirectory
        {
            Revision = historyRevision,
            Page = new Page<CommitmentDraftRevisionView>(historyRevision is not null
                ? [historyRevision] : [], null),
            RejectCursor = rejectCursor,
        });
    }

    static CommitmentDraftRevisionView Revision(Uuid tenantId, Uuid programId, Uuid draftId,
        long revision) => new(tenantId, programId, draftId, Uuid.CreateVersion4(),
        "service_commitment", "SC-01", revision, "Statement", "Context", "Source A",
        Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);

    static RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request,
        new ClaimsPrincipal());

    static ListCommitmentDraftRevisionsHandler Handler(Scenario scenario) => new(
        scenario.Directory, new CommitmentDraftHistoryReadConsistency(scenario.Directory,
            scenario.Native.Commitments));

    sealed record Scenario(Uuid TenantId, Uuid ProgramId, Uuid DraftId, CommitmentProjectionScenario Native,
        HistoryDirectory Directory) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Native.DisposeAsync();
    }

    static async Task ProjectChangeAsync(CommitmentProjectionScenario native,
        FitzCommitmentDraftHistoryDirectoryV1 directory, ICommitmentMutationContent change)
    {
        var metadata = change switch
        {
            CommitmentDraftCreatedContent created => (created.TenantId, created.DraftId, created.ActorMemberId,
                created.ActorDisplay, created.ChangedAt, nameof(CreateCommitmentDraft)),
            CommitmentDraftRevisedContent revised => (revised.TenantId, revised.DraftId, revised.ActorMemberId,
                revised.ActorDisplay, revised.ChangedAt, nameof(ReviseCommitmentDraft)),
            _ => throw new InvalidOperationException("The history fixture has another content change."),
        };
        var rules = await native.Commitments.RulesAsync(metadata.TenantId, metadata.DraftId);
        rules.Apply(change);
        var fact = await native.PublishExplicitAsync(rules, change, metadata.Item6,
            change is CommitmentDraftCreatedContent ? metadata.DraftId : Uuid.CreateVersion4(), metadata.ActorMemberId,
            metadata.ActorDisplay, metadata.Item5);
        await directory.ApplyAsync(fact);
    }

    sealed class HistoryDirectory : ICommitmentDraftHistoryDirectoryReader
    {
        public CommitmentDraftRevisionView? Revision { get; init; }
        public Page<CommitmentDraftRevisionView> Page { get; init; } = new([], null);
        public bool RejectCursor { get; init; }
        public bool ListWasCalled { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid draftId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult(Revision);

        public ValueTask<Page<CommitmentDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
            Uuid draftId, int limit, string? cursor, CancellationToken ct = default)
        {
            ListWasCalled = true;
            return RejectCursor
                ? ValueTask.FromException<Page<CommitmentDraftRevisionView>>(
                    new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }
}
