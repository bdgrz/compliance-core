using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

/// <summary>Owner-level decisions of one examination engagement, without handlers, readers or content stores.</summary>
public sealed class ExaminationEngagementAggregateTests
{
    const string Display = "Tester";
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Examination = Uuid.CreateVersion4();
    static readonly Uuid Boundary = Uuid.CreateVersion4();

    [Fact]
    public void ShouldAddressStreamByTenantAndExaminationGivenAnySelectedCommand()
    {
        // Arrange
        var plain = Fresh();
        var selected = Fresh(Uuid.CreateVersion4(), nameof(ReviseExaminationEngagement));

        // Act
        var other = new ExaminationEngagement(Tenant, Program, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(plain.Id, selected.Id);
        Assert.Equal(plain.Stream, selected.Stream);
        Assert.Equal(ExaminationEngagement.Area, plain.Stream.Area);
        Assert.Equal(Examination.ToString(), plain.Stream.Resource);
        Assert.NotEqual(plain.Stream, other.Stream);
    }

    [Fact]
    public void ShouldRaiseOpenedFactGivenFirstCreateAndRefuseSecondCreate()
    {
        // Arrange
        var owner = Fresh();
        var actor = Uuid.CreateVersion4();

        // Act
        var created = Create(owner, actor);
        var second = Create(owner, actor);

        // Assert
        Assert.True(created.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, second.Error!.Kind);
        var fact = Assert.IsType<ExaminationEngagementOpened>(Assert.Single(Pending(owner)));
        Assert.Equal(1, fact.Revision);
        Assert.Equal(Examination, fact.Operation.LogicalId);
        Assert.Null(fact.BaselineId);
        Assert.Equal("draft", owner.Header!.Status);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenCreateWithAnotherProgramOrNonTypeIOrNoBoundary()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var anotherProgram = Fresh();
        var nonTypeI = Fresh();
        var noBoundary = Fresh();

        // Act
        var program = Create(anotherProgram, actor, View(actor, 1) with { ProgramId = Uuid.CreateVersion4() });
        var type = Create(nonTypeI, actor, View(actor, 1) with { EngagementType = "type_ii" });
        var boundary = Create(noBoundary, actor, View(actor, 1) with { BoundaryId = Uuid.Empty });

        // Assert
        Assert.Equal(RequestErrorKind.Validation, program.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, type.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, boundary.Error!.Kind);
        Assert.Empty(Pending(anotherProgram));
        Assert.Empty(Pending(nonTypeI));
        Assert.Empty(Pending(noBoundary));
        Assert.Null(anotherProgram.Header);
        Assert.Null(nonTypeI.Header);
        Assert.Null(noBoundary.Header);
    }

    [Fact]
    public void ShouldAppendNothingGivenReviseOfMissingEngagement()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var revise = Uuid.CreateVersion4();
        var owner = Fresh(revise, nameof(ReviseExaminationEngagement));

        // Act
        var result = Revise(owner, revise, actor, 0);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Empty(Pending(owner));
        Assert.Null(owner.Header);
    }

    [Fact]
    public void ShouldReviseAndRefuseStaleRevisionGivenReplayedDraft()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open();
        var stale = Uuid.CreateVersion4();
        var current = Uuid.CreateVersion4();
        var staleOwner = Ready(line, stale, nameof(ReviseExaminationEngagement));
        var currentOwner = Ready(line, current, nameof(ReviseExaminationEngagement));

        // Act
        var refused = Revise(staleOwner, stale, actor, 0, 1);
        var revised = Revise(currentOwner, current, actor, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error!.Kind);
        Assert.Empty(Pending(staleOwner));
        Assert.True(revised.IsSuccess);
        var fact = Assert.IsType<ExaminationEngagementRevised>(Assert.Single(Pending(currentOwner)));
        Assert.Equal(2, fact.Revision);
        Assert.Equal(2, currentOwner.Revision);
    }

    [Fact]
    public void ShouldRefuseGivenOperationUnversionedOrNotRootedOnOwnStream()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open();
        var logical = Uuid.CreateVersion4();
        var unversioned = Ready(line, logical, nameof(ReviseExaminationEngagement));
        var foreign = Ready(line, logical, nameof(ReviseExaminationEngagement));
        var otherStream = new ExaminationEngagement(Tenant, Program, Uuid.CreateVersion4());

        // Act
        var withoutRevision = unversioned.Accept(
            NativeFacts.Root(unversioned, logical, nameof(ReviseExaminationEngagement), null, actor, Line.At(2)),
            new ExaminationEngagementState(View(actor, 2), null), Content(), Response(), ResultRef());
        var elsewhere = foreign.Accept(
            NativeFacts.Root(otherStream, logical, nameof(ReviseExaminationEngagement), 1, actor, Line.At(2)),
            new ExaminationEngagementState(View(actor, 2), null), Content(), Response(), ResultRef());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, withoutRevision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, elsewhere.Error!.Kind);
        Assert.Empty(Pending(unversioned));
        Assert.Empty(Pending(foreign));
    }

    [Fact]
    public void ShouldRefuseGivenAppendWithoutFinalizedNativeCut()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open();
        var logical = Uuid.CreateVersion4();
        var owner = NativeFacts.Replay(Fresh(logical, nameof(ReviseExaminationEngagement)), line.Facts);

        // Act
        var result = Revise(owner, logical, actor, 1);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldFreezeBaselineOnceAndRefuseReviseOrSecondFreezeGivenFrozenEngagement()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open();
        var baselineId = Uuid.CreateVersion4();
        var freezeOwner = Ready(line, baselineId, nameof(FreezeTypeIBaseline));
        var frozenLine = new Line(actor).Open().Freeze(baselineId);
        var reviseId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var reviseOwner = Ready(frozenLine, reviseId, nameof(ReviseExaminationEngagement));
        var secondOwner = Ready(frozenLine, secondId, nameof(FreezeTypeIBaseline));

        // Act
        var frozen = Freeze(freezeOwner, baselineId, actor, 1, 1);
        var revise = Revise(reviseOwner, reviseId, actor, 2);
        var second = Freeze(secondOwner, secondId, actor, 2, 2, 3);

        // Assert
        Assert.True(frozen.IsSuccess);
        var fact = Assert.IsType<TypeIBaselineFrozen>(Assert.Single(Pending(freezeOwner)));
        Assert.Equal(2, fact.Revision);
        Assert.Equal(baselineId, fact.BaselineId);
        Assert.Equal(1, fact.BaselineRevision);
        Assert.Equal(baselineId, freezeOwner.Header!.BaselineId);
        Assert.Equal("baseline_frozen", freezeOwner.Header.Status);
        Assert.Equal(RequestErrorKind.Conflict, revise.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, second.Error!.Kind);
        Assert.Empty(Pending(reviseOwner));
        Assert.Empty(Pending(secondOwner));
    }

    [Fact]
    public void ShouldAmendBaselineGivenFrozenEngagementAndRefuseAmendmentBeforeFreeze()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var draft = new Line(actor).Open();
        var frozen = new Line(actor).Open().Freeze(Uuid.CreateVersion4());
        var early = Uuid.CreateVersion4();
        var amendment = Uuid.CreateVersion4();
        var earlyOwner = Ready(draft, early, nameof(AmendTypeIBaseline));
        var amendOwner = Ready(frozen, amendment, nameof(AmendTypeIBaseline));

        // Act
        var beforeFreeze = Amend(earlyOwner, early, actor, 1, 1, 2);
        var amended = Amend(amendOwner, amendment, actor, 2, 2, 3);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, beforeFreeze.Error!.Kind);
        Assert.Empty(Pending(earlyOwner));
        Assert.True(amended.IsSuccess);
        var fact = Assert.IsType<TypeIBaselineAmended>(Assert.Single(Pending(amendOwner)));
        Assert.Equal(3, fact.Revision);
        Assert.Equal(amendment, fact.BaselineId);
        Assert.Equal(2, fact.BaselineRevision);
        Assert.Equal(2, amendOwner.Header!.BaselineRevision);
    }

    [Fact]
    public void ShouldAppendNothingGivenBaselineWhoseIdentityOrRevisionDiffersFromOperation()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open();
        var logical = Uuid.CreateVersion4();
        var otherId = Ready(line, logical, nameof(FreezeTypeIBaseline));
        var otherRevision = Ready(line, logical, nameof(FreezeTypeIBaseline));

        // Act
        var wrongId = Freeze(otherId, logical, actor, 1, 1, 2, Uuid.CreateVersion4());
        var wrongRevision = Freeze(otherRevision, logical, actor, 1, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongId.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongRevision.Error!.Kind);
        Assert.Empty(Pending(otherId));
        Assert.Empty(Pending(otherRevision));
    }

    [Fact]
    public void ShouldRestoreStateGivenValidHistoryThroughAmendment()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var baselineId = Uuid.CreateVersion4();
        var amendmentId = Uuid.CreateVersion4();
        var line = new Line(actor).Open().Revise().Freeze(baselineId).Amend(amendmentId);

        // Act
        var replayed = NativeFacts.Replay(Fresh(), line.Facts);
        var revision = replayed.Revision;
        var header = replayed.Header!;

        // Assert
        Assert.Equal(4, revision);
        Assert.Equal(amendmentId, header.BaselineId);
        Assert.Equal(2, header.BaselineRevision);
        Assert.Equal("baseline_frozen", header.Status);
        Assert.Equal(Program, replayed.ProgramId);
        Assert.IsType<TypeIBaselineAmended>(replayed.Latest);
    }

    [Fact]
    public void ShouldRecognizeExactRetryAndRefuseChangedIntentGivenCommittedFactAfterLaterFacts()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open().Revise().Freeze(Uuid.CreateVersion4());
        var revise = line.Operations[1];

        // Act
        var replayed = NativeFacts.Replay(Fresh(revise.LogicalId, revise.CommandKind), line.Facts);
        var exact = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(revise));
        var changed = replayed.Acceptance.Decide(AcceptedOperationBinding.Of(revise with
        {
            AuthoredIntentSha256 = new string('c', 64),
        }));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, exact);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
        Assert.Equal(3, replayed.Revision);
    }

    [Fact]
    public void ShouldAppendNothingGivenLogicalIdentityAlreadyAcceptedOnStream()
    {
        // Arrange
        var actor = Uuid.CreateVersion4();
        var line = new Line(actor).Open().Revise();
        var accepted = line.Operations[1].LogicalId;
        var owner = Ready(line, accepted, nameof(ReviseExaminationEngagement));

        // Act
        var result = Revise(owner, accepted, actor, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(Pending(owner));
        Assert.Equal(2, owner.Revision);
    }

    static ExaminationEngagement Fresh(Uuid selectedLogicalId = default, string? kind = null) =>
        new(Tenant, Program, Examination, selectedLogicalId, kind);

    static DomainEvent[] Pending(ExaminationEngagement owner) => new AggregateScenario<ExaminationEngagement>(owner)
        .PendingEvents.ToArray();

    /// <summary>An owner hydrated from valid history with its native cut finalized, selected for the next command.</summary>
    static ExaminationEngagement Ready(Line line, Uuid nextLogicalId, string nextKind)
    {
        var owner = NativeFacts.Replay(Fresh(nextLogicalId, nextKind), line.Facts);
        owner.ReplayCut.Confirm(owner, owner.ReplayCut.Digest);
        return owner;
    }

    static ExaminationEngagementView View(Uuid actor, long revision, string status = "draft") => new(Tenant, Program,
        Examination, "type_i", "Audit firm", new DateOnly(2026, 9, 30), null, ["security"], Boundary, [], status,
        revision, NativeFacts.Now.AddMinutes(revision), actor, Display);

    static TypeIBaselineView Baseline(Uuid actor, Uuid baselineId, long revision, long engagementRevision,
        string status) => new(Tenant, Program, Examination, baselineId, status, revision, [], [], "manifest",
        new string('d', 64), NativeFacts.Now.AddMinutes(engagementRevision), actor, Display, "Confirmed", null, null,
        [], [], 0);

    static ImmutableContentRef Content() => NativeFacts.Ref(ExaminationEngagementState.Schema);

    static ImmutableContentRef Response() => NativeFacts.Ref(FirstResponses.Schema);

    static ImmutableContentRef ResultRef() => NativeFacts.Ref("mutation-result-content.v1");

    static Result Create(ExaminationEngagement owner, Uuid actor, ExaminationEngagementView? view = null) =>
        owner.Accept(NativeFacts.Root(owner, Examination, nameof(CreateExaminationEngagement), null, actor,
            NativeFacts.Now.AddMinutes(1)), new ExaminationEngagementState(view ?? View(actor, 1), null), Content(),
        Response(), ResultRef());

    static Result Revise(ExaminationEngagement owner, Uuid logicalId, Uuid actor, long expected,
        long resultRevision = 0)
    {
        var revision = resultRevision == 0 ? expected + 1 : resultRevision;
        return owner.Accept(NativeFacts.Root(owner, logicalId, nameof(ReviseExaminationEngagement), expected, actor,
            NativeFacts.Now.AddMinutes(revision)), new ExaminationEngagementState(View(actor, revision), null),
            Content(), Response(), ResultRef());
    }

    static Result Freeze(ExaminationEngagement owner, Uuid logicalId, Uuid actor, long expected,
        long baselineRevision, long resultRevision = 0, Uuid? baselineId = null)
    {
        var revision = resultRevision == 0 ? expected + 1 : resultRevision;
        return owner.Accept(NativeFacts.Root(owner, logicalId, nameof(FreezeTypeIBaseline), expected, actor,
            NativeFacts.Now.AddMinutes(revision)),
            new ExaminationEngagementState(View(actor, revision, "baseline_frozen"),
                Baseline(actor, baselineId ?? logicalId, baselineRevision, revision, "frozen")), Content(), Response(),
            ResultRef());
    }

    static Result Amend(ExaminationEngagement owner, Uuid logicalId, Uuid actor, long expected,
        long baselineRevision, long resultRevision) => owner.Accept(
        NativeFacts.Root(owner, logicalId, nameof(AmendTypeIBaseline), expected, actor,
            NativeFacts.Now.AddMinutes(resultRevision)),
        new ExaminationEngagementState(View(actor, resultRevision, "baseline_frozen"),
            Baseline(actor, logicalId, baselineRevision, resultRevision, "amended")), Content(), Response(),
        ResultRef());

    /// <summary>Builds valid committed history directly from facts, with the exact provenance each one carries.</summary>
    sealed class Line(Uuid actor)
    {
        readonly ExaminationEngagement _template = new(Tenant, Program, Examination);
        long _baselineRevision;
        Uuid? _baselineId;

        public List<DomainEvent> Facts { get; } = [];

        public List<OperationProvenance> Operations { get; } = [];

        public static DateTimeOffset At(long revision) => NativeFacts.Now.AddMinutes(revision);

        public Line Open()
        {
            var operation = Operation(Examination, nameof(CreateExaminationEngagement), null, 1);
            Add(new ExaminationEngagementOpened(Tenant, Examination, Program, 1, "type_i", Boundary, null, 0,
                Content(), operation.AcceptedAt, operation, Response(), ResultRef(), actor), operation);
            return this;
        }

        public Line Revise()
        {
            var revision = Facts.Count + 1;
            var operation = Operation(Uuid.CreateVersion4(), nameof(ReviseExaminationEngagement), revision - 1, revision);
            Add(new ExaminationEngagementRevised(Tenant, Examination, Program, revision, "type_i", Boundary, null, 0,
                Content(), operation.AcceptedAt, operation, Response(), ResultRef(), actor), operation);
            return this;
        }

        public Line Freeze(Uuid baselineId)
        {
            var revision = Facts.Count + 1;
            var operation = Operation(baselineId, nameof(FreezeTypeIBaseline), revision - 1, revision);
            (_baselineId, _baselineRevision) = (baselineId, 1);
            Add(new TypeIBaselineFrozen(Tenant, Examination, Program, revision, "type_i", Boundary, baselineId, 1,
                Content(), operation.AcceptedAt, operation, Response(), ResultRef(), actor), operation);
            return this;
        }

        public Line Amend(Uuid baselineId)
        {
            var revision = Facts.Count + 1;
            var operation = Operation(baselineId, nameof(AmendTypeIBaseline), revision - 1, revision);
            _baselineRevision++;
            _baselineId = baselineId;
            Add(new TypeIBaselineAmended(Tenant, Examination, Program, revision, "type_i", Boundary, _baselineId,
                _baselineRevision, Content(), operation.AcceptedAt, operation, Response(), ResultRef(), actor), operation);
            return this;
        }

        OperationProvenance Operation(Uuid logicalId, string kind, long? expected, long revision) =>
            NativeFacts.Root(_template, logicalId, kind, expected, actor, At(revision));

        void Add(DomainEvent fact, OperationProvenance operation)
        {
            Facts.Add(DomainEventSeed.Attach(fact, Examination, (ulong)Facts.Count + 1));
            Operations.Add(operation);
        }
    }
}
