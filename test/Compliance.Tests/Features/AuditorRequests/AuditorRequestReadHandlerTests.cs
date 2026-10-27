using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Queries;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestReadHandlerTests
{
    [Fact]
    public async Task ShouldComposeCompleteTermsAndMaterializedDiscussionGivenNativeRequestHistory()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var native = fixture.Native;
        var revision = new ReviseAuditorRequest(Uuid.CreateVersion4(), native.TenantId, native.ProgramId,
            fixture.RequestId, 1, "Current exact wording", "Current scope", "high",
            new DateOnly(2026, 11, 30), "Clarified request.");
        var revised = await native.Handler<ReviseAuditorRequestHandler>().HandleAsync(native.Context(revision), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        var comment = new RecordAuditorRequestComment(Uuid.CreateVersion4(), native.TenantId, native.ProgramId,
            fixture.RequestId, "Complete internal discussion.", "internal", 2);
        var admitted = await native.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(native.Context(comment), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, (await native.MaterializeAsync(comment.CommentId)).State);

        // Act
        var result = await fixture.GetAsync(fixture.RequestId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(native.TenantId, result.Value.TenantId);
        Assert.Equal(native.ProgramId, result.Value.ProgramId);
        Assert.Equal(native.ExaminationId, result.Value.ExaminationId);
        Assert.Equal("Current exact wording", result.Value.Wording);
        Assert.Equal(2, result.Value.TermsHistory.Count);
        Assert.Equal("External request wording", result.Value.TermsHistory[0].Wording);
        var discussion = Assert.Single(result.Value.Comments);
        Assert.Equal(comment.CommentId, discussion.CommentId);
        Assert.Equal("Complete internal discussion.", discussion.Body);
        Assert.Equal("internal", discussion.Compartment);
        Assert.Equal(native.MemberId, discussion.ActorMemberId);
        Assert.Empty(result.Value.Responses);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnNotFoundGivenAnotherTenantOrVerifiedProgram(bool anotherTenant)
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();

        // Act
        var result = await fixture.GetAsync(fixture.RequestId,
            tenant: anotherTenant ? Uuid.CreateVersion4() : fixture.Native.TenantId,
            program: anotherTenant ? fixture.Native.ProgramId : Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldFenceIdentityDiscoveryAndComposeCurrentBodiesGivenCaughtUpProjection()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var second = await fixture.RecordAsync("Second request");
        var behind = await fixture.ListAsync();
        await fixture.ProjectAsync();
        var native = fixture.Native;
        var changed = new ReviseAuditorRequest(Uuid.CreateVersion4(), native.TenantId, native.ProgramId,
            fixture.RequestId, 1, "Current exact wording", "Current scope", "high",
            new DateOnly(2026, 11, 30), "Current lifecycle body.");
        var revised = await native.Handler<ReviseAuditorRequestHandler>().HandleAsync(native.Context(changed), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        var stale = await fixture.ListAsync();
        await fixture.ProjectAsync();

        // Act
        var first = await fixture.ListAsync(limit: 1);
        var last = await fixture.ListAsync(limit: 1, cursor: first.Value.NextCursor);
        var rows = first.Value.Items.Concat(last.Value.Items).ToArray();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, behind.Error?.Kind);
        Assert.True(behind.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(stale.Error!.IsTransient);
        Assert.NotNull(first.Value.NextCursor);
        Assert.Null(last.Value.NextCursor);
        Assert.Equal(2, rows.Select(static row => row.AuditorRequestId).Distinct().Count());
        Assert.Contains(rows, row => row.AuditorRequestId == second);
        Assert.Equal("Current exact wording", rows.Single(row => row.AuditorRequestId == fixture.RequestId).Name);
        Assert.DoesNotContain(rows, row => row.Status == "created_identity");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("limit")]
    [InlineData("malformed")]
    public async Task ShouldRejectCursorGivenAnotherAuthorizedDiscoveryScope(string mismatch)
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        await fixture.RecordAsync("Second request");
        await fixture.ProjectAsync();
        var first = await fixture.ListAsync(limit: 1);

        // Act
        var result = await fixture.ListAsync(limit: mismatch == "limit" ? 2 : 1,
            cursor: mismatch == "malformed" ? "invalid-cursor" : first.Value.NextCursor,
            tenant: mismatch == "tenant" ? Uuid.CreateVersion4() : fixture.Native.TenantId,
            program: mismatch == "program" ? Uuid.CreateVersion4() : fixture.Native.ProgramId);

        // Assert
        Assert.NotNull(first.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefusePartialDirectoryPageGivenSourceAppendOrCheckpointMovementDuringRead(bool advanceCheckpoint)
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        await fixture.ProjectAsync();
        var hook = new HookedDirectory(fixture.Directory, async () =>
        {
            await fixture.RecordAsync("Concurrent request");
            if (advanceCheckpoint)
                await fixture.ProjectAsync();
        });

        // Act
        var result = await fixture.ListAsync(directory: hook);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldIgnoreUnrelatedTenantSourceGivenExactAuditorRequestCheckpointPattern()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        await fixture.ProjectAsync();
        var program = new ProgramRules(fixture.Native.TenantId, Uuid.CreateVersion4());
        Assert.Null(program.Create("Unrelated program", new ProgramPlan(null, null, null, null, null, null),
            fixture.Native.MemberId, "Program author", AuditorRequestFixture.Now));
        await ProgramSeeds.WriteAsync(fixture.Native.Reader, fixture.Native.Writer, fixture.Native.Events, fixture.Native.Content, program);

        // Act
        var result = await fixture.ListAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.RequestId, Assert.Single(result.Value.Items).AuditorRequestId);
    }

    [Fact]
    public async Task ShouldWaitForAdmittedChildThenReadExactFrozenSelectionGivenCaughtUpLocator()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var population = await fixture.Native.SeedFrozenPopulationAsync();
        var selection = Uuid.CreateVersion4();
        await fixture.SelectAsync(fixture.RequestId, selection, population.PopulationId, population.IncludedRowId);
        var behind = await fixture.SelectionAsync(selection);
        await fixture.ProjectAsync();
        var pending = await fixture.SelectionAsync(selection);
        Assert.Equal(ProcessState.Completed, (await fixture.Native.MaterializeAsync(selection)).State);

        // Act
        var result = await fixture.SelectionAsync(selection);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, behind.Error?.Kind);
        Assert.True(behind.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, pending.Error?.Kind);
        Assert.True(pending.Error!.IsTransient);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.RequestId, result.Value.AuditorRequestId);
        Assert.Equal(population.PopulationId, result.Value.PopulationSnapshotId);
        Assert.Equal(1, result.Value.PopulationVersion);
        Assert.Equal(population.ContentSha256, result.Value.PopulationContentSha256);
        Assert.Equal(population.IncludedRowId, Assert.Single(result.Value.SelectedRowIds));
        Assert.Equal("external_email", result.Value.Source);
        Assert.Equal("sample-source", result.Value.ExternalReference);
    }

    [Fact]
    public async Task ShouldRetainAmbiguityGivenSameLocalSelectionAcceptedByTwoRequestOwners()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var second = await fixture.RecordAsync("Another request");
        var population = await fixture.Native.SeedFrozenPopulationAsync();
        var sharedSelection = Uuid.CreateVersion4();
        await fixture.SelectAsync(fixture.RequestId, sharedSelection, population.PopulationId, population.IncludedRowId);
        await fixture.SelectAsync(second, sharedSelection, population.PopulationId, population.IncludedRowId);
        await fixture.ProjectAsync();

        // Act
        var rows = await fixture.Directory.FindSelectionAsync(fixture.Native.TenantId, fixture.Native.ProgramId, sharedSelection);
        var result = await fixture.SelectionAsync(sharedSelection);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(static row => row.AuditorRequestId).Distinct().Count());
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Contains("more than one", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyUnavailableAndCorruptNativeReadContentGivenExistingRequest(bool unavailable)
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var native = fixture.Native;
        var requests = new AuditorRequestReader(native.Reader, native.Events, new FaultedContent(native.Content, unavailable));
        var handler = new GetAuditorRequestHandler(requests);

        // Act
        var result = await handler.HandleAsync(native.Context(new GetAuditorRequest(native.TenantId, native.ProgramId,
            fixture.RequestId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldListOnlyCurrentSubmittedVersionsGivenDraftAndRetainedSubmissionHistory(bool newDraft)
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var native = fixture.Native;
        var submitted = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        Assert.True((await native.DraftAsync(fixture.RequestId, submitted)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await native.MaterializeAsync(submitted)).State);
        Assert.True((await native.SubmitAsync(fixture.RequestId, submitted)).IsSuccess);
        Assert.True((await native.DraftAsync(fixture.RequestId, draft)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await native.MaterializeAsync(draft)).State);
        if (newDraft)
        {
            var rejected = await native.ReviewAsync(fixture.RequestId, submitted, "rejected");
            Assert.True(rejected.IsSuccess, rejected.Error?.Message);
            var response = await native.ResponseAsync(fixture.RequestId, submitted);
            var revised = await native.Handler<ReviseAuditResponseDraftHandler>().HandleAsync(native.Context(
                new ReviseAuditResponseDraft(Uuid.CreateVersion4(), native.TenantId, native.ProgramId,
                    fixture.RequestId, submitted, response.Revision, "New unsubmitted current version.", [])), CancellationToken.None);
            Assert.True(revised.IsSuccess, revised.Error?.Message);
        }

        // Act
        var result = await fixture.ResponsesAsync();
        var full = await fixture.GetAsync(fixture.RequestId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(full.IsSuccess, full.Error?.Message);
        Assert.Equal(2, full.Value.Responses.Count);
        if (newDraft)
        {
            Assert.Empty(result.Value.Items);
            Assert.Contains(full.Value.Responses.Single(row => row.ResponseId == submitted).Versions,
                version => version.SubmittedAt is not null);
        }
        else
        {
            var row = Assert.Single(result.Value.Items);
            Assert.Equal(submitted, row.AuditorRequestResponseId);
            Assert.Equal(fixture.RequestId, row.AuditorRequestId);
            Assert.NotEqual(default, row.SubmittedAt);
            Assert.Null(row.ArtifactId);
        }
    }

    [Fact]
    public async Task ShouldRejectResponseContinuationGivenCurrentSubmittedSetChanged()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var native = fixture.Native;
        var responses = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        foreach (var response in responses)
        {
            Assert.True((await native.DraftAsync(fixture.RequestId, response)).IsSuccess);
            Assert.Equal(ProcessState.Completed, (await native.MaterializeAsync(response)).State);
            Assert.True((await native.SubmitAsync(fixture.RequestId, response)).IsSuccess);
        }
        var first = await fixture.ResponsesAsync(limit: 1);
        Assert.NotNull(first.Value.NextCursor);
        var third = Uuid.CreateVersion4();
        Assert.True((await native.DraftAsync(fixture.RequestId, third)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await native.MaterializeAsync(third)).State);
        Assert.True((await native.SubmitAsync(fixture.RequestId, third)).IsSuccess);

        // Act
        var changed = await fixture.ResponsesAsync(limit: 1, cursor: first.Value.NextCursor);
        var wrongScope = AuditorRequestResponsePaging.Create(Uuid.CreateVersion4(), native.ProgramId, fixture.RequestId,
            first.Value.Items, 1, first.Value.NextCursor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.True(changed.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Validation, wrongScope.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRollbackIdentityWritesGivenContradictoryNativeCreationInProjectionBatch()
    {
        // Arrange
        await using var fixture = await ReadFixture.CreateAsync();
        var source = (await fixture.Native.RequestAsync(fixture.RequestId)).Recorded!;
        var contradictory = source with { SubjectId = Uuid.CreateVersion4() };
        contradictory.AttachMetadata(source.Metadata);
        var identity = new CheckpointIdentity(FitzAuditorRequestDirectory.ProjectorName,
            EventStreamPattern.ForPattern(fixture.Native.TenantId.ToString(), AuditorRequest.Area));
        var before = await fixture.Directory.LoadCheckpointAsync(fixture.Native.TenantId);
        Exception? failure;

        // Act
        await using (var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(identity, before)))
        {
            await fixture.Directory.ApplyAsync(source);
            failure = await Record.ExceptionAsync(async () => await fixture.Directory.ApplyAsync(contradictory));
        }
        var rows = await fixture.Directory.ListProgramAsync(fixture.Native.TenantId, fixture.Native.ProgramId, 50, null);
        var after = await fixture.Directory.LoadCheckpointAsync(fixture.Native.TenantId);

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Empty(rows.Items);
        Assert.Equal(before, after);
    }

    sealed class ReadFixture : IAsyncDisposable
    {
        public AuditorRequestFixture Native { get; } = new();
        public FitzAuditorRequestDirectory Directory { get; }
        public AuditorRequestReader Requests { get; }
        public Uuid RequestId { get; private set; }

        ReadFixture()
        {
            Directory = new FitzAuditorRequestDirectory(new PersistenceKvClient(new InMemoryKvClient()), Native.Content);
            Requests = new AuditorRequestReader(Native.Reader, Native.Events, Native.Content);
        }

        public static async Task<ReadFixture> CreateAsync()
        {
            var fixture = new ReadFixture();
            fixture.RequestId = await fixture.Native.SeedRequestAsync();
            return fixture;
        }

        public async Task<Uuid> RecordAsync(string wording)
        {
            var request = Native.NewRecordCommand() with { Wording = wording };
            var result = await Native.Handler<RecordAuditorRequestHandler>().HandleAsync(Native.Context(request), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            return request.RequestId;
        }

        public async Task SelectAsync(Uuid request, Uuid selection, Uuid population, Uuid row)
        {
            var owner = await Native.RequestAsync(request);
            var command = new RecordSampleSelection(Native.TenantId, Native.ProgramId, Native.ExaminationId,
                request, owner.Revision, selection, population, 1, [row], "random", "external_email", "sample-source");
            var result = await Native.Handler<RecordSampleSelectionHandler>().HandleAsync(Native.Context(command), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        public Task<Result<AuditorRequestView>> GetAsync(Uuid request, Uuid? tenant = null, Uuid? program = null) =>
            new GetAuditorRequestHandler(Requests).HandleAsync(Native.Context(new GetAuditorRequest(tenant ?? Native.TenantId,
                program ?? Native.ProgramId, request)), CancellationToken.None).AsTask();

        public Task<Result<Page<AuditorRequestSummaryView>>> ListAsync(int? limit = null, string? cursor = null,
            Uuid? tenant = null, Uuid? program = null, IAuditorRequestDirectoryReader? directory = null)
        {
            var source = directory ?? Directory;
            return new ListAuditorRequestsHandler(source, new AuditorRequestDirectoryReadConsistency(source, Native.Events), Requests)
                .HandleAsync(Native.Context(new ListAuditorRequests(tenant ?? Native.TenantId, program ?? Native.ProgramId,
                    limit, cursor)), CancellationToken.None).AsTask();
        }

        public Task<Result<SampleSelectionView>> SelectionAsync(Uuid selection) => new GetSampleSelectionHandler(Directory,
            new AuditorRequestDirectoryReadConsistency(Directory, Native.Events), Requests).HandleAsync(Native.Context(
            new GetSampleSelection(Native.TenantId, Native.ProgramId, selection)), CancellationToken.None).AsTask();

        public Task<Result<Page<AuditorRequestResponseView>>> ResponsesAsync(int? limit = null, string? cursor = null) =>
            new ListRequestResponsesHandler(Requests).HandleAsync(Native.Context(new ListRequestResponses(Native.TenantId,
                Native.ProgramId, RequestId, limit, cursor)), CancellationToken.None).AsTask();

        public async Task ProjectAsync()
        {
            var checkpoint = await Directory.LoadCheckpointAsync(Native.TenantId);
            var identity = new CheckpointIdentity(FitzAuditorRequestDirectory.ProjectorName,
                EventStreamPattern.ForPattern(Native.TenantId.ToString(), AuditorRequest.Area));
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Native.Events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                if (record.Event is AuditorRequestRecorded created)
                    await Directory.ApplyAsync(created);
                if (record.Event is AuditorSampleSelectionAdmitted selection)
                    await Directory.ApplyAsync(selection);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public ValueTask DisposeAsync() => Native.DisposeAsync();
    }

    sealed class HookedDirectory(IAuditorRequestDirectoryReader inner, Func<Task> afterPage) : IAuditorRequestDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
            inner.LoadCheckpointAsync(tenantId, ct);
        public async ValueTask<Page<AuditorRequestSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var result = await inner.ListProgramAsync(tenantId, programId, limit, cursor, ct);
            await afterPage();
            return result;
        }
        public ValueTask<IReadOnlyList<AuditorSampleSelectionLocator>> FindSelectionAsync(Uuid tenantId, Uuid programId,
            Uuid selectionId, CancellationToken ct = default) => inner.FindSelectionAsync(tenantId, programId, selectionId, ct);
    }

    sealed class FaultedContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact source body is not visible yet.")
                : new ImmutableContentIntegrityException("The exact source body is corrupt."));
    }
}
