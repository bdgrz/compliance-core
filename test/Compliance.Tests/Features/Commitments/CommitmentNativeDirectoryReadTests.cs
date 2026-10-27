using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Handlers;
using Bdgrz.Compliance.Features.Commitments.Queries;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentNativeDirectoryReadTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReadCompleteLargeContentGivenCompactNativeDirectoryRows()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var draft = CommitmentDraft.IdFor(tenant, program, "service_commitment", "SC-LARGE");
        var rules = new CommitmentRules(tenant, draft);
        var text = new string('"', 16000);
        Assert.True(rules.Create(program, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", "SC-LARGE",
            text, text, "MSA 4.1", Uuid.CreateVersion4(), "Author", Now).IsSuccess);
        var facts = await native.PublishAsync(rules);
        var directory = new FitzCommitmentDraftDirectory(new PersistenceKvClient(native.Kv), native.Commitments);
        var history = new FitzCommitmentDraftHistoryDirectoryV1(new PersistenceKvClient(native.Kv), native.Commitments);

        // Act
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, tenant, facts);
        await native.ProjectAsync(history, history.ApplyAsync, FitzCommitmentDraftHistoryDirectoryV1.ProjectorName, tenant, facts);
        var current = await directory.GetAsync(tenant, draft);
        var original = await history.GetRevisionAsync(tenant, draft, 1);
        var page = await directory.ListProgramAsync(tenant, program, 1, null);

        // Assert
        Assert.Equal(text, current!.Statement);
        Assert.Equal(text, current.Context);
        Assert.Equal(text, original!.Statement);
        Assert.Equal(text, original.Context);
        Assert.Equal(text, Assert.Single(page.Items).Statement);
        Assert.All(facts, fact => Assert.True(DomainEventPayload.Size(fact) < DomainEventPayload.MaximumBytes));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholePageGivenUnreadableNativeCommitmentContent(bool unavailable)
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var programSource = new ProgramRules(tenant, program);
        Assert.Null(programSource.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Program lead", Now));
        await ProgramSeeds.WriteAsync(native.Reader, native.Writer, native.Events, native.Content, programSource);
        var first = await SeedAsync(native, tenant, program, "SC-01");
        var second = await SeedAsync(native, tenant, program, "SC-02");
        var normal = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);
        var checkpoint = await CheckpointAsync(native, tenant);
        await native.ProjectAsync(normal, normal.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, tenant,
            [.. first, .. second], checkpoint);
        var target = Assert.IsAssignableFrom<ICommitmentFact>(Assert.Single(second)).Header.Content;
        var missing = new UnreadableContent(native.Content, target.ContentId, unavailable);
        var sources = new CommitmentReader(native.Reader, native.Events, missing);
        var directory = new FitzCommitmentDraftDirectory(native.Kv, sources);
        var handler = new ListCommitmentDraftsHandler(directory, native.Reader,
            new CommitmentDraftListReadConsistency(directory, native.Events));

        // Act
        var result = await handler.HandleAsync(Context(new ListCommitmentDrafts(tenant, program)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Equal(1, missing.RejectedReads);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("tenant")]
    public async Task ShouldRejectCrossScopeCursorGivenNativeIdentityDiscovery(string changed)
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var first = await SeedAsync(native, tenant, program, "SC-01");
        var second = await SeedAsync(native, tenant, program, "SC-02");
        var directory = new FitzCommitmentDraftDirectory(native.Kv, native.Commitments);
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzCommitmentDraftDirectory.ProjectorName, tenant,
            [.. first, .. second]);
        var page = await directory.ListProgramAsync(tenant, program, 1, null);

        // Act
        var rejected = await Record.ExceptionAsync(async () => await directory.ListProgramAsync(
            changed == "tenant" ? Uuid.CreateVersion4() : tenant,
            changed == "program" ? Uuid.CreateVersion4() : program, 1, page.NextCursor));
        var continued = await directory.ListProgramAsync(tenant, program, 1, page.NextCursor);
        var foreign = await directory.GetAsync(Uuid.CreateVersion4(),
            CommitmentDraft.IdFor(tenant, program, "service_commitment", "SC-01"));

        // Assert
        Assert.NotNull(page.NextCursor);
        Assert.IsType<KvDirectoryQueryException>(rejected);
        Assert.Single(continued.Items);
        Assert.NotEqual(Assert.Single(page.Items).DraftId, continued.Items[0].DraftId);
        Assert.Null(foreign);
    }

    static async Task<IReadOnlyList<DomainEvent>> SeedAsync(CommitmentProjectionScenario native, Uuid tenant,
        Uuid program, string identifier)
    {
        var draft = CommitmentDraft.IdFor(tenant, program, "service_commitment", identifier);
        var rules = new CommitmentRules(tenant, draft);
        Assert.True(rules.Create(program, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", identifier,
            "Protect customer data", "Security commitment", "MSA 4.1", Uuid.CreateVersion4(), "Author", Now).IsSuccess);
        return await native.PublishAsync(rules);
    }
    static async Task<ProjectionCheckpoint> CheckpointAsync(CommitmentProjectionScenario native, Uuid tenant)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in native.Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), CommitmentDraft.Area),
            EventCursor.Start, CancellationToken.None))
            cursor = record.NextCursor;
        return new(cursor);
    }
    static Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request, new ClaimsPrincipal());

    sealed class UnreadableContent(IImmutableContentStore inner, Uuid target, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.ContentId != target)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("The commitment body is not visible yet.")
                : new ImmutableContentIntegrityException("The commitment body differs from its frozen digest.");
        }
    }
}
