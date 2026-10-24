using Microsoft.Extensions.DependencyInjection;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ControlTypeIBaselineSourceContributorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnClassifiedSourceFailureGivenUnreadableNativeControlContent(bool unavailable)
    {
        // Arrange
        var control = ApprovedControl("AC-UNREADABLE", new DateOnly(2026, 9, 1));
        await using var source = await NativeSource.CreateAsync(control);
        var before = await source.Controls.ReadAsync(TenantId, ProgramId, control.Id);
        var contributor = Contributor(new Directory([control]), source, source.WithReadFailure(unavailable));

        // Act
        var result = await contributor.ReadAsync(Engagement(new DateOnly(2026, 9, 30)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        var retained = await source.Controls.ReadAsync(TenantId, ProgramId, control.Id);
        Assert.Equal(before.Owner.CommittedStreamPosition, retained.Owner.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldBindControlVersionEffectiveOnTypeIDateGivenApprovedSuccessor()
    {
        // Arrange
        var control = ApprovedControl("AC-01", new DateOnly(2026, 9, 1));
        await using var source = await NativeSource.CreateAsync(control);
        var predecessor = Assert.IsType<ControlVersionView>(control.ApprovedVersion);
        var directory = new Directory([control]);
        var contributor = Contributor(directory, source);
        var originalPreview = await contributor.ReadAsync(Engagement(new DateOnly(2026, 10, 14)),
            CancellationToken.None);
        ApproveSuccessor(control, new DateOnly(2026, 10, 15));
        await source.AppendAsync(control);
        directory.Replace(View(control));
        var successor = Assert.IsType<ControlVersionView>(control.ApprovedVersion);

        // Act
        var beforeSuccessor = await contributor.ReadAsync(Engagement(new DateOnly(2026, 10, 14)),
            CancellationToken.None);
        var onSuccessor = await contributor.ReadAsync(Engagement(new DateOnly(2026, 10, 15)),
            CancellationToken.None);

        // Assert
        Assert.True(originalPreview.IsSuccess, originalPreview.Error?.Message);
        Assert.True(beforeSuccessor.IsSuccess, beforeSuccessor.Error?.Message);
        Assert.True(onSuccessor.IsSuccess, onSuccessor.Error?.Message);
        var original = Assert.Single(originalPreview.Value.Sources);
        var before = Assert.Single(beforeSuccessor.Value.Sources);
        Assert.Equal("controls", beforeSuccessor.Value.Family);
        Assert.Equal("control_version", before.Kind);
        Assert.Equal(control.Id, before.RecordId);
        Assert.Equal(predecessor.VersionId, before.VersionId);
        Assert.Equal(predecessor.Revision, before.Revision);
        Assert.Equal(original.ContentSha256, before.ContentSha256);
        Assert.Matches("^[0-9a-f]{64}$", before.ContentSha256);
        var current = Assert.Single(onSuccessor.Value.Sources);
        Assert.Equal(successor.VersionId, current.VersionId);
        Assert.Equal(successor.Revision, current.Revision);
        Assert.NotEqual(before.ContentSha256, current.ContentSha256);
        Assert.Empty(beforeSuccessor.Value.Blockers);
        Assert.Empty(onSuccessor.Value.Blockers);
    }

    [Fact]
    public async Task ShouldOmitControlWithoutVersionEffectiveOnTypeIDateGivenFutureInitialApproval()
    {
        // Arrange
        var future = ApprovedControl("AC-FUTURE", new DateOnly(2026, 10, 1));
        await using var source = await NativeSource.CreateAsync(future);
        var contributor = Contributor(new Directory([future]), source);

        // Act
        var result = await contributor.ReadAsync(Engagement(new DateOnly(2026, 9, 30)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Empty(result.Value.Blockers);
        Assert.Single(result.Value.ScopeDifferences);
    }

    [Fact]
    public async Task ShouldRejectBaselineGivenControlDirectoryChangesDuringRead()
    {
        // Arrange
        var control = ApprovedControl("AC-02", new DateOnly(2026, 9, 1));
        await using var source = await NativeSource.CreateAsync(control);
        var directory = new AdvancingDirectory(View(control));
        var contributor = Contributor(directory, source);

        // Act
        var result = await contributor.ReadAsync(Engagement(new DateOnly(2026, 9, 30)),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectBaselineGivenControlFromAnotherProgramInDirectory()
    {
        // Arrange
        var control = ApprovedControl("AC-03", new DateOnly(2026, 9, 1));
        await using var source = await NativeSource.CreateAsync(control);
        var wrongProgramView = View(control) with { ProgramId = Uuid.CreateVersion4() };
        var contributor = Contributor(new Directory([wrongProgramView]), source);

        // Act
        var result = await contributor.ReadAsync(Engagement(new DateOnly(2026, 9, 30)),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    static ControlTypeIBaselineSourceContributor Contributor(
        IControlDraftDirectoryReader directory, NativeSource source, ControlReader? controls = null)
    {
        if (directory is Directory stable)
            stable.CurrentCheckpoint = () => source.Checkpoint;
        if (directory is AdvancingDirectory advancing)
            advancing.InitialCheckpoint = source.Checkpoint;
        return new(directory, controls ?? source.Controls, new ControlDraftListReadConsistency(directory, source.Events));
    }

    static ExaminationEngagementView Engagement(DateOnly periodStart) => new(TenantId, ProgramId,
        Uuid.CreateVersion4(), "type_i", "Audit Firm", periodStart, null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, Now, AuthorId, "Examiner");

    static ControlRules ApprovedControl(string identifier, DateOnly effectiveFrom)
    {
        var id = ControlDraft.IdFor(TenantId, ProgramId, identifier);
        var control = new ControlRules(TenantId, id);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            AuthorId, "Author", Now).IsSuccess);
        ApproveDraft(control, effectiveFrom, Now.AddMinutes(1));
        return control;
    }

    static void ApproveSuccessor(ControlRules control, DateOnly effectiveFrom)
    {
        Assert.Null(control.ProposeSuccessor(ProgramId, control.ApprovedVersion!.VersionId,
            Content() with { Title = "Access review successor" }, AuthorId, "Author",
            Now.AddHours(1)));
        ApproveDraft(control, effectiveFrom, Now.AddHours(2), "impact-digest");
    }

    static void ApproveDraft(ControlRules control, DateOnly effectiveFrom,
        DateTimeOffset decisionAt, string? impactDigest = null)
    {
        var scope = new ResponsibilityScope("control", control.Id, control.DraftVersionId,
            control.Revision);
        Assert.Null(control.AssignResponsibility(scope, Uuid.CreateVersion4(), OwnerId,
            ResponsibilityType.ControlOwner, AuthorId, "Author", decisionAt,
            Now.AddMinutes(-1), null, []));
        var review = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, control.Revision, review, "accept", "Reviewed.",
            ReviewerId, "Reviewer", decisionAt.AddMinutes(1)));
        Assert.Null(control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(), review,
            effectiveFrom, "Approved.", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            decisionAt.AddMinutes(2), impactDigest: impactDigest));
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.",
        ["Dated review record"]);

    static ControlDraftView View(ControlRules control) => new(TenantId, ProgramId, control.Id,
        control.ApprovedVersion?.Identifier ?? control.Id.ToString(), control.Revision,
        control.IsApproved ? "approved" : "draft",
        "declared_unverified", "unresolved", control.CurrentContent!, AuthorId, "Author", Now)
    {
        DraftVersionId = control.DraftVersionId,
    };

    sealed class Directory : IControlDraftDirectoryReader
    {
        ControlDraftView[] _views;
        public Func<ProjectionCheckpoint> CurrentCheckpoint { get; set; } = () => ProjectionCheckpoint.Start;

        public Directory(IEnumerable<ControlRules> controls) : this(controls.Select(View).ToArray())
        {
        }

        public Directory(ControlDraftView[] views) => _views = views;

        public void Replace(ControlDraftView view) => _views = [view];

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(CurrentCheckpoint());

        public ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
            CancellationToken ct = default) => ValueTask.FromResult(_views.FirstOrDefault(view =>
            view.TenantId == tenantId && view.ControlId == controlId));

        public ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var items = _views.Where(view => view.TenantId == tenantId).ToArray();
            return ValueTask.FromResult(new Page<ControlDraftView>(items, null));
        }

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid controlId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ControlDraftRevisionView?>(null);
    }

    sealed class AdvancingDirectory(ControlDraftView view) : IControlDraftDirectoryReader
    {
        int _loads;
        public ProjectionCheckpoint InitialCheckpoint { get; set; } = ProjectionCheckpoint.Start;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(++_loads == 1
            ? InitialCheckpoint
            : new ProjectionCheckpoint(new EventCursor("advanced")));

        public ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
            CancellationToken ct = default) => ValueTask.FromResult<ControlDraftView?>(null);

        public ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(
            new Page<ControlDraftView>([view], null));

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid controlId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<ControlDraftRevisionView?>(null);
    }

    sealed class FailingReads(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<Bdgrz.Compliance.Features.Proofs.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm,
            Bdgrz.Compliance.Features.Proofs.ImmutableContentRef reference, CancellationToken ct = default) =>
            Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The selected content is not visible yet.")
                : new ImmutableContentIntegrityException("The selected content does not verify."));
    }

    sealed class NativeSource : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateReader _reader;
        readonly IAggregateWriter _writer;
        readonly IImmutableContentStore _content;
        public InMemoryEventStore Events { get; }
        public ControlReader Controls { get; }
        public ProjectionCheckpoint Checkpoint { get; private set; } = ProjectionCheckpoint.Start;

        NativeSource()
        {
            var services = new ServiceCollection();
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            _reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            _content = _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            Events = events;
            Controls = new(_reader, events, _content);
        }

        public ControlReader WithReadFailure(bool unavailable) =>
            new(_reader, Events, new FailingReads(_content, unavailable));

        public static async Task<NativeSource> CreateAsync(ControlRules control)
        {
            var source = new NativeSource();
            await source.AppendAsync(control);
            return source;
        }

        public async Task AppendAsync(ControlRules control)
        {
            await ControlSeeds.AppendAsync(_reader, _writer, _content, control, CancellationToken.None);
            var cursor = EventCursor.Start;
            await foreach (var record in Events.ReadAsync(EventStreamPattern.ForPattern(
                               TenantId.ToString(), "controls"), cursor, CancellationToken.None))
                cursor = record.NextCursor;
            Checkpoint = new(cursor);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
