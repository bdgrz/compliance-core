using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class BoundaryNativeAuthorityTests
{
    [Fact]
    public async Task ShouldRetainExactLargeContentAndOriginalActorGivenCreateRetryAfterDisplayChange()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var references = new References();
        var request = new CreateBoundary(scenario.Id, scenario.Tenant, scenario.Program,
            Content(new string('<', 24000)));
        var handler = new CreateBoundaryHandler(scenario.Source.Operations(), references);
        var first = await handler.HandleAsync(BoundarySourceFixture.Context(request, scenario.Author,
            display: "original.author@example.test"), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var accepted = await scenario.ReadAsync();
        references.Reject = true;
        scenario.Source.Clock.Now = scenario.Source.Clock.Now.AddDays(1);

        // Act
        var retry = await handler.HandleAsync(BoundarySourceFixture.Context(request, scenario.Author,
            display: "renamed.author@example.test"), CancellationToken.None);
        var retained = await scenario.ReadAsync();

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(accepted.SourcePhysicalPosition, retained.SourcePhysicalPosition);
        Assert.Equal(request.Content.Statement, retained.Draft!.Content.Statement);
        Assert.Equal(1, references.Reads);
        var fact = Assert.IsType<BoundaryDraftCreated>(Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id)));
        Assert.Equal("original.author@example.test", fact.Operation.Actor.Display);
        Assert.Equal("original.author@example.test", fact.AuthorDisplay);
        Assert.True(DomainEventPayload.Size(fact) <= DomainEventPayload.MaximumBytes);
        var original = await FirstResponses.ReplayAsync(scenario.Source.Content, scenario.Tenant.ToString(), fact.FirstResponse!, CancellationToken.None);
        Assert.Equal(200, original.HttpStatus);
        var wrapper = await scenario.Source.Content.ReadJsonAsync<MutationResultContent>(scenario.Tenant.ToString(), fact.OperationResult!);
        Assert.Equal(original.ResultSchema, wrapper.Schema);
        Assert.Equal(original.Body, wrapper.Value);
    }

    [Fact]
    public async Task ShouldReplayOriginalRevisionGivenLaterEditAndUnreadableMutablePreparation()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var references = new References();
        var first = new ReviseBoundaryDraft(Uuid.CreateVersion4(), scenario.Tenant, scenario.Id,
            scenario.Version, 1, Content("First changed scope"));
        var handler = new ReviseBoundaryDraftHandler(scenario.Source.Operations(), references);
        Assert.True((await handler.HandleAsync(BoundarySourceFixture.Context(first, scenario.Author), CancellationToken.None)).IsSuccess);
        var later = first with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, Content = Content("Later changed scope") };
        Assert.True((await handler.HandleAsync(BoundarySourceFixture.Context(later, scenario.Author), CancellationToken.None)).IsSuccess);
        var accepted = await scenario.ReadAsync();
        references.Reject = true;
        scenario.Source.Clock.Now = scenario.Source.Clock.Now.AddDays(2);
        var replayHandler = new ReviseBoundaryDraftHandler(scenario.Source.Operations(new UnreadableChange(scenario.Source.Content, true)), references);

        // Act
        var retry = await replayHandler.HandleAsync(BoundarySourceFixture.Context(first, scenario.Author), CancellationToken.None);
        var changedIntent = await replayHandler.HandleAsync(BoundarySourceFixture.Context(first with { Content = Content("Other content") }, scenario.Author), CancellationToken.None);
        var changedActor = await replayHandler.HandleAsync(BoundarySourceFixture.Context(first, scenario.Reviewer), CancellationToken.None);
        var changedGate = await replayHandler.HandleAsync(BoundarySourceFixture.Context(first with { ExpectedRevision = 2 }, scenario.Author), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedGate.Error?.Kind);
        var retained = await scenario.ReadAsync();
        Assert.Equal(accepted.SourcePhysicalPosition, retained.SourcePhysicalPosition);
        Assert.Equal(3, retained.DraftRevision);
        Assert.Equal("Later changed scope", retained.Draft!.Content.Statement);
        Assert.Equal(2, references.Reads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyExactNativeContentFailureGivenNewHandlerPreparation(bool unavailable)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var request = new ReviseBoundaryDraft(Uuid.CreateVersion4(), scenario.Tenant, scenario.Id, scenario.Version, 1, Content("Changed"));
        var handler = new ReviseBoundaryDraftHandler(scenario.Source.Operations(new UnreadableChange(scenario.Source.Content, unavailable)), new References());
        var before = await scenario.ReadAsync();

        // Act
        var result = await handler.HandleAsync(BoundarySourceFixture.Context(request, scenario.Author), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(before.SourcePhysicalPosition, (await scenario.ReadAsync()).SourcePhysicalPosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectPreparedMutationGivenReviewOrAssignmentAtSameDraftRevision(bool assignment)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var before = await scenario.ReadAsync();
        var prepared = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        Assert.Null(prepared.Revise(scenario.Version, 1, Content("Prepared edit"), scenario.AuthorMember, "Author", scenario.Source.Clock.Now));
        var mutation = new BoundaryMutationContent(prepared.PendingChanges);
        var command = Uuid.CreateVersion4();
        var provenance = OwnerProvenance.For(before.Owner, command, nameof(ReviseBoundaryDraft),
            ActorReference.ForMember(scenario.AuthorMember, "Author"), scenario.Id.ToString(), 1,
            AuthoredIntentDigest.Compute(mutation), scenario.Source.Clock.Now);
        var fact = await BoundarySeeds.FreezeAsync(scenario.Source.Content, prepared, mutation, provenance, before.Owner.NativeSequence + 1);
        var current = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        if (assignment)
            Assert.Null(current.AssignResponsibility(new ResponsibilityScope("boundary", scenario.Id, scenario.Version, 1), Uuid.CreateVersion4(),
                scenario.AuthorMember, ResponsibilityType.ControlOwner, scenario.AuthorMember, "Author", scenario.Source.Clock.Now,
                scenario.Source.Clock.Now, null, []));
        else
            Assert.Null(current.Review(scenario.Version, 1, Uuid.CreateVersion4(), "accept", "Independent review",
                scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(current);
        var advanced = await scenario.ReadAsync();

        // Act
        var refused = advanced.Owner.Append(fact, before.Owner.NativeSequence, before.SourcePhysicalPosition,
            mutation, before.Rules.Authority(before.Owner.Header!), prepared.Authority(((IBoundaryFact)fact).Header));

        // Assert
        Assert.Equal(1, advanced.DraftRevision);
        Assert.True(advanced.SourcePhysicalPosition > before.SourcePhysicalPosition);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("authority")]
    public async Task ShouldRejectMalformedNativeAcceptanceGivenForgedActorGateOrCurrentAuthority(string fault)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var before = await scenario.ReadAsync();
        var rules = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var decision = Uuid.CreateVersion4();
        Assert.Null(rules.Review(scenario.Version, 1, decision, "accept", "Independent review", scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
        var mutation = new BoundaryMutationContent(rules.PendingChanges);
        var operation = OwnerProvenance.For(before.Owner, decision, nameof(ReviewBoundary), ActorReference.ForMember(scenario.ReviewerMember, "Reviewer"),
            scenario.Id.ToString(), 1, AuthoredIntentDigest.Compute(mutation), scenario.Source.Clock.Now);
        var fact = Assert.IsType<BoundaryReviewed>(await BoundarySeeds.FreezeAsync(scenario.Source.Content, rules, mutation, operation, before.Owner.NativeSequence + 1));
        var after = rules.Authority(fact.Header);
        if (fault == "actor")
            fact = fact with { ActorMemberId = scenario.AuthorMember };
        else if (fault == "revision")
            fact = fact with { Operation = operation with { OriginalExpectedRevision = 2 } };
        else
        {
            after = after with { DecisionConstraints = [] };
            fact = fact with { Authority = await scenario.Source.Content.WriteJsonAsync(scenario.Tenant.ToString(), Uuid.CreateVersion4(), BoundaryFactContent.AuthoritySchema, after) };
        }

        // Act
        var result = before.Owner.Append(fact, before.Owner.NativeSequence, before.SourcePhysicalPosition, mutation,
            before.Rules.Authority(before.Owner.Header!), after);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before.SourcePhysicalPosition, (await scenario.ReadAsync()).SourcePhysicalPosition);
    }

    [Fact]
    public async Task ShouldFreezeExactWaiverAndReplayOriginalReviewGivenExpiryAfterAcceptance()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var scope = new SeparationOfDutiesWaiverScope("boundary", scenario.Id, scenario.Version, 1, SeparationOfDutiesActions.Review);
        var waiver = new SeparationOfDutiesWaiver(scenario.Tenant, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, scenario.AuthorMember, Uuid.CreateVersion4(), "Requester", "Exact independent exception",
            scenario.Source.Clock.Now.AddMinutes(-2), scenario.Source.Clock.Now.AddHours(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Independent approver", scenario.Source.Clock.Now.AddMinutes(-1)));
        await scenario.Source.Writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var request = new ReviewBoundary(Uuid.CreateVersion4(), scenario.Tenant, scenario.Id, scenario.Version, 1,
            "accept", "Reviewed under the approved waiver", waiver.Id);
        var handler = new ReviewBoundaryHandler(scenario.Source.Operations());
        Assert.True((await handler.HandleAsync(BoundarySourceFixture.Context(request, scenario.Author, http: true), CancellationToken.None)).IsSuccess);
        var before = await scenario.ReadAsync();
        scenario.Source.Clock.Now = scenario.Source.Clock.Now.AddDays(1);

        // Act
        var retry = await handler.HandleAsync(BoundarySourceFixture.Context(request, scenario.Author, http: true), CancellationToken.None);
        var fresh = await handler.HandleAsync(BoundarySourceFixture.Context(request with { CommandId = Uuid.CreateVersion4() },
            scenario.Author, http: true), CancellationToken.None);
        var fact = Assert.IsType<BoundaryReviewed>((await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id))[^1]);
        var mutation = await BoundaryFactContent.ReadChangeAsync(scenario.Source.Content, fact);
        var proof = Assert.Single(mutation.Waivers!);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, fresh.Error?.Kind);
        Assert.Equal(before.SourcePhysicalPosition, (await scenario.ReadAsync()).SourcePhysicalPosition);
        Assert.Equal(waiver.Id, proof.WaiverId);
        Assert.Equal(scenario.AuthorMember, proof.ActorId);
        Assert.Equal(scenario.AuthorMember, proof.SubjectId);
        Assert.Equal(scope.Action, proof.Action);
        Assert.Equal(scope.RecordType, proof.TargetType);
        Assert.Equal(scope.RecordId, proof.TargetId);
        Assert.Equal(scope.VersionId, proof.TargetVersionId);
        Assert.Equal(scope.Revision, proof.TargetRevision);
        Assert.Equal(waiver.Stream, proof.ApprovalFact.Source);
        Assert.Equal(1UL, proof.ApprovalFact.ResourcePosition);
        Assert.Equal(2UL, proof.ApprovalFact.AggregateVersion);
    }

    [Fact]
    public async Task ShouldPreserveExactApprovedHistoryGivenManyLargeSuccessorVersions()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var rules = new BoundaryRules(scenario.Tenant, scenario.Id);
        Assert.True(rules.Create(scenario.Program, scenario.Version, Content("Version 1" + new string('<', 24000)),
            scenario.AuthorMember, "Author", scenario.Source.Clock.Now).IsSuccess);
        var first = scenario.Version;
        var current = first;
        var approver = RbacIds.Member(scenario.Tenant, Uuid.CreateVersion4());
        for (var version = 1; version <= 12; version++)
        {
            if (version > 1)
            {
                var command = Uuid.CreateVersion4();
                var next = Uuid.CreateVersion5(command, "draft");
                Assert.Null(rules.ProposeSuccessor(current, next, Content("Version " + version + new string('<', 24000)),
                    scenario.AuthorMember, "Author", scenario.Source.Clock.Now, command));
                current = next;
            }
            var review = Uuid.CreateVersion4();
            Assert.Null(rules.Review(current, 1, review, "accept", "Independent review", scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
            Assert.Null(rules.Approve(current, 1, Uuid.CreateVersion4(), review, new DateOnly(2026, version, 1),
                "Approved", "impact-digest", approver, "Approver", scenario.Source.Clock.Now));
        }

        // Act
        await scenario.Source.SeedAsync(rules);
        var snapshot = await scenario.ReadAsync();
        var facts = await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id);

        // Assert
        Assert.Equal(12, snapshot.ReadVersions().Count);
        Assert.Equal(first, snapshot.FirstApprovedVersionId);
        Assert.Equal(current, snapshot.LatestApprovedVersionId);
        Assert.Equal(first, snapshot.EffectiveVersion(new DateOnly(2026, 1, 15))!.VersionId);
        Assert.Equal("Version 1" + new string('<', 24000), snapshot.FindVersion(first)!.Content.Statement);
        Assert.All(facts, fact => Assert.True(DomainEventPayload.Size(fact) <= DomainEventPayload.MaximumBytes));
        Assert.Null(snapshot.Draft);
    }

    [Fact]
    public async Task ShouldRequireLatestAcceptedReviewGivenLaterReviewAfterReplay()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var rules = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var first = Uuid.CreateVersion4();
        var latest = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        Assert.Null(rules.Review(scenario.Version, 1, first, "accept", "First review",
            scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
        Assert.Null(rules.Review(scenario.Version, 1, Uuid.CreateVersion4(), "request_changes", "Further changes needed",
            scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(rules);
        var reloaded = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var before = await scenario.ReadAsync();

        // Act
        var rejected = reloaded.Approve(scenario.Version, 1, Uuid.CreateVersion4(), first, new DateOnly(2027, 1, 1),
            "Approved", "impact", approver, "Approver", scenario.Source.Clock.Now);
        Assert.Null(reloaded.Review(scenario.Version, 1, latest, "accept", "Reviewed again",
            scenario.ReviewerMember, "Reviewer", scenario.Source.Clock.Now));
        var stale = reloaded.Approve(scenario.Version, 1, Uuid.CreateVersion4(), first, new DateOnly(2027, 1, 1),
            "Approved", "impact", approver, "Approver", scenario.Source.Clock.Now);
        var approved = reloaded.Approve(scenario.Version, 1, Uuid.CreateVersion4(), latest, new DateOnly(2027, 1, 1),
            "Approved", "impact", approver, "Approver", scenario.Source.Clock.Now);

        // Assert
        Assert.Equal("Approval requires the latest accepted review of this draft revision.", rejected?.Message);
        Assert.Equal("Approval requires the latest accepted review of this draft revision.", stale?.Message);
        Assert.Null(approved);
        Assert.Equal(2, reloaded.PendingChanges.Count);
        Assert.Equal(before.SourcePhysicalPosition, (await scenario.ReadAsync()).SourcePhysicalPosition);
        await scenario.Source.SeedAsync(reloaded);
        Assert.Equal(scenario.Version, (await scenario.ReadAsync()).LatestApprovedVersionId);
    }

    [Fact]
    public async Task ShouldRejectRetroactiveAssignmentGivenEarlierReviewAfterReplay()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync(seed: true);
        var rules = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var firstReview = scenario.Source.Clock.Now.AddMinutes(1);
        Assert.Null(rules.Review(scenario.Version, 1, Uuid.CreateVersion4(), "request_changes", "First review",
            scenario.ReviewerMember, "First reviewer", firstReview));
        Assert.Null(rules.Review(scenario.Version, 1, Uuid.CreateVersion4(), "accept", "Second review",
            Uuid.CreateVersion4(), "Second reviewer", firstReview.AddMinutes(1)));
        await scenario.Source.SeedAsync(rules);
        var reloaded = await scenario.Source.Boundaries.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var scope = new ResponsibilityScope("boundary", scenario.Id, scenario.Version, 1);
        var before = await scenario.ReadAsync();

        // Act
        var result = reloaded.AssignResponsibility(scope, Uuid.CreateVersion4(), scenario.ReviewerMember,
            ResponsibilityType.ControlOwner, scenario.AuthorMember, "Author", firstReview.AddMinutes(2),
            firstReview.AddMinutes(-1), firstReview.AddTicks(1), []);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("The responsibility would create an unwaived conflict with a prior decision on this exact revision.", result.Message);
        Assert.Empty(reloaded.PendingChanges);
        Assert.Equal(before.SourcePhysicalPosition, (await scenario.ReadAsync()).SourcePhysicalPosition);
    }

    static BoundaryContent Content(string statement) => new(statement, "type_i", ["security"], []);
    sealed class References : IBoundaryReferenceValidator
    {
        public bool Reject { get; set; }
        public int Reads { get; private set; }
        public ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content, CancellationToken ct = default)
        {
            Reads++;
            return ValueTask.FromResult(Reject ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The current references changed.")) : Result.Success);
        }
    }
    sealed class UnreadableChange(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != BoundaryFactContent.ChangeSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            throw unavailable ? new ImmutableContentUnavailableException("The exact boundary change is not visible yet.")
                : new ImmutableContentIntegrityException("The exact boundary change digest differs.");
        }
    }
    sealed class Scenario : IAsyncDisposable
    {
        public BoundarySourceFixture Source { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Id { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Version => Uuid.CreateVersion5(Id, "draft-1");
        public Uuid AuthorMember => RbacIds.Member(Tenant, Author);
        public Uuid ReviewerMember => RbacIds.Member(Tenant, Reviewer);
        public static async Task<Scenario> CreateAsync(bool seed = false)
        {
            var scenario = new Scenario();
            await scenario.Source.SeedProgramAsync(scenario.Tenant, scenario.Program, scenario.Author);
            if (seed)
            {
                var rules = new BoundaryRules(scenario.Tenant, scenario.Id);
                Assert.True(rules.Create(scenario.Program, scenario.Version, Content("Original scope"), scenario.AuthorMember, "Author", scenario.Source.Clock.Now).IsSuccess);
                await scenario.Source.SeedAsync(rules);
            }
            return scenario;
        }
        public ValueTask<BoundaryReadSnapshot> ReadAsync() => Source.Boundaries.ReadAsync(Tenant, Program, Id);
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }
}
