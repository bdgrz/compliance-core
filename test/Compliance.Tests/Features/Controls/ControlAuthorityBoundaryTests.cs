using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlAuthorityBoundaryTests
{
    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("target")]
    [InlineData("outcome")]
    [InlineData("supersedes")]
    [InlineData("header")]
    [InlineData("operation")]
    public async Task ShouldRejectMalformedNativeReviewGivenExactPreparedContent(string defect)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.RulesAsync();
        var before = rules.Authority(owner.Header!);
        var reviewer = Uuid.CreateVersion4();
        var decision = Uuid.CreateVersion4();
        var operation = scenario.Operation(owner, decision, nameof(ReviewControl), reviewer, 1);
        var change = new ControlReviewedContent(scenario.Tenant, scenario.Program, scenario.Id,
            rules.DraftVersionId, 1, decision, "accept", reviewer, "Reviewer", "Verified", scenario.Source.Clock.Now);
        change = defect switch
        {
            "actor" => change with { ActorMemberId = Uuid.CreateVersion4() },
            "target" => change with { VersionId = Uuid.CreateVersion4() },
            "outcome" => change with { Outcome = "approve" },
            "supersedes" => change with { SupersedesDecisionId = Uuid.CreateVersion4() },
            _ => change,
        };
        if (defect == "revision")
            operation = operation with { OriginalExpectedRevision = 7 };
        rules.Apply(change);
        var mutation = new ControlMutationContent([change]);
        var fact = Assert.IsType<ControlReviewed>(await ControlSeeds.FreezeAsync(scenario.Source.Content,
            rules, mutation.Changes, operation, owner.NativeSequence + 1));
        if (defect == "header")
            fact = fact with { Header = fact.Header with { PendingAuthorMemberId = reviewer } };
        if (defect == "operation")
            fact = fact with { Operation = null! };
        var physical = owner.CommittedStreamPosition;

        // Act
        var result = owner.Append(fact, owner.NativeSequence, physical, mutation, before, rules.Authority(fact.Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Equal(physical, owner.CommittedStreamPosition);
        Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldConflictWithoutAppendGivenPreparationBeforeSameRevisionAuthorityChange(bool assignment)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var preparedOwner = await scenario.OwnerAsync();
        var preparedRules = await scenario.RulesAsync();
        var before = preparedRules.Authority(preparedOwner.Header!);
        var reviewer = Uuid.CreateVersion4();
        var decision = Uuid.CreateVersion4();
        Assert.Null(preparedRules.Review(scenario.Program, 1, decision, "accept", "Prepared review",
            reviewer, "Reviewer", scenario.Source.Clock.Now));
        var mutation = new ControlMutationContent(preparedRules.PendingChanges.ToArray());
        var fact = await ControlSeeds.FreezeAsync(scenario.Source.Content, preparedRules, mutation.Changes,
            scenario.Operation(preparedOwner, decision, nameof(ReviewControl), reviewer, 1), preparedOwner.NativeSequence + 1);
        var concurrent = await scenario.RulesAsync();
        if (assignment)
            Assert.Null(concurrent.AssignResponsibility(new ResponsibilityScope(SeparationOfDutiesRecordTypes.Control,
                scenario.Id, concurrent.DraftVersionId, 1), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                ResponsibilityType.ControlOwner, scenario.Author, "Author", scenario.Source.Clock.Now,
                scenario.Source.Clock.Now, null, []));
        else
            Assert.Null(concurrent.Review(scenario.Program, 1, Uuid.CreateVersion4(), "request_changes", "Concurrent review",
                Uuid.CreateVersion4(), "Other reviewer", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(concurrent);
        var current = await scenario.OwnerAsync();
        var physical = current.CommittedStreamPosition;

        // Act
        var result = current.Append(fact, preparedOwner.NativeSequence, preparedOwner.CommittedStreamPosition, mutation, before, preparedRules.Authority(((IControlFact)fact).Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Equal(1, current.Revision);
        Assert.Equal(2, current.NativeSequence);
        Assert.Equal(physical, current.CommittedStreamPosition);
        Assert.Equal(2, (await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectPoisonedAfterAuthorityGivenVerifiedNativeChange(bool substituteBody)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.RulesAsync();
        var before = rules.Authority(owner.Header!);
        var reviewer = Uuid.CreateVersion4();
        var decision = Uuid.CreateVersion4();
        Assert.Null(rules.Review(scenario.Program, 1, decision, "accept", "Verified", reviewer, "Reviewer", scenario.Source.Clock.Now));
        var mutation = new ControlMutationContent(rules.PendingChanges.ToArray());
        var fact = Assert.IsType<ControlReviewed>(await ControlSeeds.FreezeAsync(scenario.Source.Content, rules, mutation.Changes,
            scenario.Operation(owner, decision, nameof(ReviewControl), reviewer, 1), 2));
        var after = rules.Authority(fact.Header);
        if (substituteBody)
        {
            after = after with { DecisionConstraints = [] };
            var poison = await scenario.Source.Content.WriteJsonAsync(scenario.Tenant.ToString(), Uuid.CreateVersion4(),
                ControlFactContent.AuthoritySchema, after);
            fact = fact with { Authority = poison };
        }
        else
            fact = fact with { Authority = owner.CurrentAuthority! };

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, mutation, before, after);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id));
    }

    [Fact]
    public async Task ShouldRejectChangedNoOpReceiptGivenUnchangedAuthorityHeader()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.RulesAsync();
        var before = rules.Authority(owner.Header!);
        var receipt = new ControlDraftRevisedContent(scenario.Tenant, scenario.Program, scenario.Id, 1,
            Scenario.Content("Different authored content"), scenario.Author, "Author", scenario.Source.Clock.Now);
        var mutation = new ControlMutationContent([], Receipt: receipt);
        var frozen = await ControlContent.FreezeAsync(scenario.Source.Content, rules, mutation);
        var previous = Assert.IsType<ControlDraftCreated>(Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id)));
        var fact = ControlFactFactory.Create(nameof(ReviseControlDraft), frozen.Header, owner.NativeSequence + 1,
            scenario.Operation(owner, Uuid.CreateVersion4(), nameof(ReviseControlDraft), scenario.Author, 1),
            frozen.Change, frozen.Authority, previous.FirstResponse!, previous.OperationResult!, mutation);

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, mutation, before, rules.Authority(((IControlFact)fact).Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id));
    }

    [Fact]
    public async Task ShouldRejectOversizedScalarEnvelopeGivenValidImmutableDraft()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.RulesAsync();
        var before = rules.Authority(owner.Header!);
        Assert.Null(rules.Discard(scenario.Program, 1, new string('x', 52 * 1024), scenario.Author,
            "Author", scenario.Source.Clock.Now));
        var mutation = new ControlMutationContent(rules.PendingChanges.ToArray());
        var fact = await ControlSeeds.FreezeAsync(scenario.Source.Content, rules, mutation.Changes,
            scenario.Operation(owner, Uuid.CreateVersion4(), nameof(DiscardControlDraft), scenario.Author, 1), 2);

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, mutation, before, rules.Authority(((IControlFact)fact).Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.True(owner.IsVisible);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Single(await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id));
    }

    [Fact]
    public async Task ShouldRejectFurtherMutationGivenDiscardedNativeAuthority()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var rules = await scenario.RulesAsync();
        Assert.Null(rules.Discard(scenario.Program, 1, "Unused draft", scenario.Author, "Author", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(rules);
        var owner = await scenario.OwnerAsync();
        rules = await scenario.RulesAsync();
        var before = rules.Authority(owner.Header!);
        var change = new ControlDraftRevisedContent(scenario.Tenant, scenario.Program, scenario.Id, 2,
            Scenario.Content("Attempted resurrection"), scenario.Author, "Author", scenario.Source.Clock.Now);
        rules.Apply(change);
        var mutation = new ControlMutationContent([change]);
        var fact = await ControlSeeds.FreezeAsync(scenario.Source.Content, rules, mutation.Changes,
            scenario.Operation(owner, Uuid.CreateVersion4(), nameof(ReviseControlDraft), scenario.Author, 1), 3);

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, mutation, before, rules.Authority(((IControlFact)fact).Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.False(owner.IsVisible);
        Assert.Equal(2, owner.NativeSequence);
        Assert.Equal(2, (await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id)).Count);
    }

    [Fact]
    public async Task ShouldRetainExistingDecisionPolicyGivenSameIndependentReviewerAndApprover()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var rules = await scenario.RulesAsync();
        var ownerMember = Uuid.CreateVersion4();
        var decisionMember = Uuid.CreateVersion4();
        var review = Uuid.CreateVersion4();
        Assert.Null(rules.AssignResponsibility(new ResponsibilityScope(SeparationOfDutiesRecordTypes.Control,
            scenario.Id, rules.DraftVersionId, 1), Uuid.CreateVersion4(), ownerMember, ResponsibilityType.ControlOwner,
            scenario.Author, "Author", scenario.Source.Clock.Now, scenario.Source.Clock.Now, null, []));
        Assert.Null(rules.Review(scenario.Program, 1, review, "accept", "Reviewed", decisionMember,
            "Independent decision member", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(rules);
        rules = await scenario.RulesAsync();

        // Act
        Assert.Null(rules.Approve(scenario.Program, 1, Uuid.CreateVersion4(), review, new DateOnly(2026, 10, 24),
            "Approved", new HashSet<Uuid> { ownerMember }, decisionMember, "Independent decision member", scenario.Source.Clock.Now));
        await scenario.Source.SeedAsync(rules);
        var current = await scenario.Source.Controls.ReadAsync(scenario.Tenant, scenario.Program, scenario.Id);

        // Assert
        Assert.True(current.IsApproved);
        Assert.False(current.HasOpenDraft);
        Assert.Equal(4, current.Owner.NativeSequence);
        Assert.Equal(decisionMember.ToString(), Assert.Single(current.ReadVersions()).ApprovedBy.Id);
    }

    [Theory]
    [InlineData("punctuation")]
    [InlineData("length")]
    public async Task ShouldRejectInvalidIdentifierGivenOtherwiseExactNativeCreation(string invalid)
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var identifier = invalid == "length" ? new string('A', 81) : "AC:INVALID";
        var id = ControlDraft.IdFor(tenant, program, identifier);
        var owner = new ControlDraft(tenant, id);
        var content = Scenario.Content("Valid immutable authored body");
        var change = new ControlDraftCreatedContent(tenant, program, id, id, identifier, content,
            actor, "Author", source.Clock.Now);
        var rules = new ControlRules(tenant, id);
        // Explicit fixture folding keeps the invalid identifier. All native body/ref/authority bindings are real.
        rules.Apply(change);
        var mutation = new ControlMutationContent([change]);
        var operation = OwnerProvenance.For(owner, id, nameof(CreateControlDraft),
            ActorReference.ForMember(actor, actor.ToString()), id.ToString(), null,
            AuthoredIntentDigest.Compute(new CreateControlDraft(tenant, program, identifier, content)), source.Clock.Now);
        var fact = await ControlSeeds.FreezeAsync(source.Content, rules, mutation.Changes, operation, 1);
        var native = Assert.IsAssignableFrom<IControlFact>(fact);

        // Act
        var result = owner.Append(fact, 0, 0, mutation, null, rules.Authority(native.Header));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(owner.IsCreated);
        Assert.Equal(0, owner.NativeSequence);
        Assert.Empty(await source.FactsAsync(tenant, id));
    }

    sealed class Scenario : IAsyncDisposable
    {
        public ControlSourceFixture Source { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Id => ControlDraft.IdFor(Tenant, Program, "AUTHORITY-BOUNDARY");
        public static async Task<Scenario> CreateAsync()
        {
            var scenario = new Scenario();
            var rules = new ControlRules(scenario.Tenant, scenario.Id);
            Assert.True(rules.Create(scenario.Program, scenario.Id, "AUTHORITY-BOUNDARY", Content("Original"),
                scenario.Author, "Author", scenario.Source.Clock.Now).IsSuccess);
            await scenario.Source.SeedAsync(rules);
            return scenario;
        }
        public async Task<ControlDraft> OwnerAsync() => await Source.Reader.HydrateAsync(new ControlDraft(Tenant, Id));
        public async Task<ControlRules> RulesAsync() => await Source.Controls.RulesAsync(Tenant, Program, Id);
        public OperationProvenance Operation(ControlDraft owner, Uuid id, string kind, Uuid actor, ulong? revision) =>
            OwnerProvenance.For(owner, id, kind, ActorReference.ForMember(actor, actor.ToString()), owner.Id.ToString(),
                revision, AuthoredIntentDigest.Compute(new ReviewControl(id, Tenant, Program, Id, 1, "accept", "Prepared")), Source.Clock.Now);
        public static ControlDraftContent Content(string title) => new(title, "Review access", "Management reviews access",
            "The security lead reviews access quarterly", ["Dated review record"]);
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }
}
