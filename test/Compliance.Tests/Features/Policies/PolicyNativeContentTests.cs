using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Handlers;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class PolicyNativeContentTests
{
    [Fact]
    public async Task ShouldRetainOriginalActorDisplayGivenNativePolicyAdmissionAndLaterLabelChange()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = Policy.IdFor(tenant, program, "NATIVE-ACTOR");
        var request = new CreatePolicyDraft(id, tenant, program, "NATIVE-ACTOR", Content("Original actor"));
        var handler = new CreatePolicyDraftHandler(source.Operations());
        Cntryl.Portia.RequestContext<CreatePolicyDraft> Context(string email) => new(request,
            new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"),
                new Claim("sub", user.ToString()), new Claim("email", email)], "UnitTest"))));

        // Act
        var first = await handler.HandleAsync(Context("original@example.com"), CancellationToken.None);
        var retry = await handler.HandleAsync(Context("renamed@example.com"), CancellationToken.None);
        var fact = Assert.IsType<PolicyDraftCreated>(Assert.Single(await source.FactsAsync(tenant, id)));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal("original@example.com", fact.Operation.Actor.Display);
        Assert.Equal("original@example.com", fact.Actor.Display);
    }

    [Fact]
    public async Task ShouldRetainVerifiedHistoryOutsideCompactOwnerGivenManyNativePolicyRevisions()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = Policy.IdFor(tenant, program, "NATIVE-HISTORY");
        var create = new CreatePolicyDraft(id, tenant, program, "NATIVE-HISTORY", Content("Revision 1"));
        Assert.True((await new CreatePolicyDraftHandler(source.Operations()).HandleAsync(
            PolicySourceFixture.Context(create, author), CancellationToken.None)).IsSuccess);
        var revisions = new RevisePolicyDraftHandler(source.Operations());

        // Act
        for (var revision = 2; revision <= 70; revision++)
        {
            source.Clock.Now = source.Clock.Now.AddMinutes(1);
            var result = await revisions.HandleAsync(PolicySourceFixture.Context(new RevisePolicyDraft(Uuid.CreateVersion4(),
                tenant, program, id, revision - 1, Content($"Revision {revision}")), author), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        var owner = await source.Reader.HydrateAsync(new Policy(tenant, id));
        var facts = await source.FactsAsync(tenant, id);
        var first = await PolicyFactContent.ReadChangeAsync(source.Content, Assert.IsType<PolicyDraftCreated>(facts[0]));
        var current = await source.Policies.ReadAsync(tenant, program, id);

        // Assert
        Assert.Equal(70, owner.NativeSequence);
        Assert.Equal(70, owner.Revision);
        Assert.Equal("Revision 1", Assert.IsType<PolicyDraftCreatedContent>(first.Change).Content.Title);
        Assert.Equal("Revision 70", current.DraftContent!.Title);
        Assert.Equal(70, facts.Count);
        Assert.All(facts, fact => Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!).Length < 8 * 1024));
        Assert.DoesNotContain("Revision 1", JsonSerializer.Serialize(owner.Header,
            ComplianceCoreJsonContext.Default.PolicyAuthorityHeader), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayOriginalResponseBeforeMutablePreparationGivenUnreadablePolicyHistory(bool unavailable)
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = Policy.IdFor(tenant, program, "NATIVE-RETRY");
        var create = new CreatePolicyDraft(id, tenant, program, "NATIVE-RETRY", Content("First"));
        var first = await new CreatePolicyDraftHandler(source.Operations()).HandleAsync(
            PolicySourceFixture.Context(create, author), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var revision = new RevisePolicyDraft(Uuid.CreateVersion4(), tenant, program, id, 1, Content("Second"));
        var revisions = new RevisePolicyDraftHandler(source.Operations());
        var accepted = await revisions.HandleAsync(PolicySourceFixture.Context(revision, author), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True((await revisions.HandleAsync(PolicySourceFixture.Context(new RevisePolicyDraft(Uuid.CreateVersion4(),
            tenant, program, id, 2, Content("Third")), author), CancellationToken.None)).IsSuccess);
        var before = await source.Reader.HydrateAsync(new Policy(tenant, id));
        var missing = new UnreadableSchema(source.Content, PolicyFactContent.ChangeSchema, unavailable);
        var handler = new RevisePolicyDraftHandler(source.Operations(missing));

        // Act
        var replay = await handler.HandleAsync(PolicySourceFixture.Context(revision, author), CancellationToken.None);
        var creation = await new CreatePolicyDraftHandler(source.Operations(missing)).HandleAsync(
            PolicySourceFixture.Context(create, author), CancellationToken.None);
        var changedActor = await handler.HandleAsync(PolicySourceFixture.Context(revision, Uuid.CreateVersion4()), CancellationToken.None);
        var changedIntent = await handler.HandleAsync(PolicySourceFixture.Context(revision with { Content = Content("Different") }, author), CancellationToken.None);
        var changedGate = await handler.HandleAsync(PolicySourceFixture.Context(revision with { ExpectedRevision = 2 }, author), CancellationToken.None);
        Assert.Equal(0, missing.RejectedReads);
        var fresh = await handler.HandleAsync(PolicySourceFixture.Context(new RevisePolicyDraft(Uuid.CreateVersion4(),
            tenant, program, id, 3, Content("Fourth")), author), CancellationToken.None);
        var after = await source.Reader.HydrateAsync(new Policy(tenant, id));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.True(creation.IsSuccess, creation.Error?.Message);
        Assert.Equal(accepted.Value, replay.Value);
        Assert.Equal(first.Value, creation.Value);
        Assert.Equal(2, replay.Value.Revision);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedGate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fresh.Error?.Kind);
        Assert.Equal(unavailable, fresh.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(1, missing.RejectedReads);
    }

    [Fact]
    public async Task ShouldFreezeExactPolicyWaiverAndReplayOriginalDecisionGivenLaterExpiry()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, author);
        var id = Policy.IdFor(tenant, program, "NATIVE-WAIVER");
        Assert.True((await new CreatePolicyDraftHandler(source.Operations()).HandleAsync(PolicySourceFixture.Context(
            new CreatePolicyDraft(id, tenant, program, "NATIVE-WAIVER", Content("Exact waiver")), author), CancellationToken.None)).IsSuccess);
        var handler = new ReviewPolicyDraftHandler(source.Operations());
        var denied = await handler.HandleAsync(PolicySourceFixture.Context(new ReviewPolicyDraft(Uuid.CreateVersion4(), tenant,
            program, id, 1, "accept", "Self review without waiver"), author, http: true), CancellationToken.None);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Policy, id, id, 1, SeparationOfDutiesActions.Review);
        var waiver = new SeparationOfDutiesWaiver(tenant, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, member, Uuid.CreateVersion4(), "Requester", "Exact independent exception",
            source.Clock.Now.AddMinutes(-2), source.Clock.Now.AddHours(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Independent approver", source.Clock.Now.AddMinutes(-1)));
        await source.Writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var request = new ReviewPolicyDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "accept", "Exact authorized exception", waiver.Id);
        var accepted = await handler.HandleAsync(PolicySourceFixture.Context(request, author, http: true), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var before = await source.Reader.HydrateAsync(new Policy(tenant, id));
        source.Clock.Now = source.Clock.Now.AddDays(1);

        // Act
        var replay = await handler.HandleAsync(PolicySourceFixture.Context(request, author, http: true), CancellationToken.None);
        var fresh = await handler.HandleAsync(PolicySourceFixture.Context(request with { CommandId = Uuid.CreateVersion4() },
            author, http: true), CancellationToken.None);
        var fact = Assert.IsType<PolicyReviewed>((await source.FactsAsync(tenant, id))[^1]);
        var mutation = await PolicyFactContent.ReadChangeAsync(source.Content, fact);
        var proof = Assert.IsType<SeparationOfDutiesWaiverProof>(mutation.Waiver);
        var after = await source.Reader.HydrateAsync(new Policy(tenant, id));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(accepted.Value, replay.Value);
        Assert.Equal(RequestErrorKind.Forbidden, fresh.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(waiver.Id, proof.WaiverId);
        Assert.Equal(tenant, proof.TenantId);
        Assert.Equal(member, proof.ActorId);
        Assert.Equal(member, proof.SubjectId);
        Assert.Equal(scope.Action, proof.Action);
        Assert.Equal(scope.RecordType, proof.TargetType);
        Assert.Equal(scope.RecordId, proof.TargetId);
        Assert.Equal(scope.VersionId, proof.TargetVersionId);
        Assert.Equal(scope.Revision, proof.TargetRevision);
        Assert.Equal(waiver.Stream, proof.ApprovalFact.Source);
        Assert.Equal(1UL, proof.ApprovalFact.ResourcePosition);
        Assert.Equal(2UL, proof.ApprovalFact.AggregateVersion);
        Assert.Equal(waiver.ApprovedAt, proof.ValidFrom);
        Assert.Equal(waiver.ExpiresAt, proof.ValidUntil);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("actor_snapshot")]
    [InlineData("gate")]
    [InlineData("authority")]
    [InlineData("verified_wrong_authority")]
    public async Task ShouldRejectInvalidNativePolicyBindingGivenExactPreparedSourceCut(string defect)
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var id = Policy.IdFor(tenant, program, "NATIVE-BOUNDARY");
        var rules = new PolicyRules(tenant, id);
        Assert.True(rules.Create(program, id, "NATIVE-BOUNDARY", Content("Initial"),
            ActorReference.ForMember(actor, "Author"), actor, source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        var owner = await source.Reader.HydrateAsync(new Policy(tenant, id));
        rules = await source.Policies.RulesAsync(tenant, program, id);
        var before = rules.Authority(owner.Header!);
        var reviewer = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        Assert.Null(rules.Review(program, 1, command, "accept", "Reviewed", ActorReference.ForMember(reviewer, "Reviewer"),
            reviewer, source.Clock.Now));
        var change = rules.PendingChanges[0];
        var operation = OwnerProvenance.For(owner, command, nameof(ReviewPolicyDraft), ActorReference.ForMember(reviewer, reviewer.ToString()),
            id.ToString(), 1, AuthoredIntentDigest.Compute(new ReviewPolicyDraft(command, tenant, program, id, 1, "accept", "Reviewed")), source.Clock.Now);
        if (defect == "gate")
            operation = operation with { OriginalExpectedRevision = 9 };
        var fact = Assert.IsType<PolicyReviewed>(await PolicySeeds.FreezeAsync(source.Content, rules, change, operation, 2));
        var after = rules.Authority(fact.Header);
        if (defect == "actor")
            fact = fact with { ActorMemberId = actor };
        if (defect == "actor_snapshot")
            fact = fact with { Actor = ActorReference.ForMember(actor, "Another member") };
        if (defect == "authority")
            fact = fact with { Authority = owner.CurrentAuthority! };
        if (defect == "verified_wrong_authority")
        {
            after = after with { PendingAuthorMemberIds = [] };
            fact = fact with
            {
                Authority = await source.Content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                PolicyFactContent.AuthoritySchema, after)
            };
        }

        // Act
        var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition, new PolicyMutationContent(change), before, after);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(1, owner.NativeSequence);
        Assert.Single(await source.FactsAsync(tenant, id));
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenPreparationBeforeSameRevisionPolicyReview()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = Policy.IdFor(tenant, program, "NATIVE-CUT");
        var rules = new PolicyRules(tenant, id);
        Assert.True(rules.Create(program, id, "NATIVE-CUT", Content("Exact cut"), ActorReference.ForMember(author, "Author"),
            author, source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        var preparedOwner = await source.Reader.HydrateAsync(new Policy(tenant, id));
        rules = await source.Policies.RulesAsync(tenant, program, id);
        var before = rules.Authority(preparedOwner.Header!);
        var reviewer = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        Assert.Null(rules.Review(program, 1, command, "accept", "Prepared", ActorReference.ForMember(reviewer, "Reviewer"),
            reviewer, source.Clock.Now));
        var mutation = new PolicyMutationContent(rules.PendingChanges[0]);
        var operation = OwnerProvenance.For(preparedOwner, command, nameof(ReviewPolicyDraft),
            ActorReference.ForMember(reviewer, reviewer.ToString()), id.ToString(), 1,
            AuthoredIntentDigest.Compute(new ReviewPolicyDraft(command, tenant, program, id, 1, "accept", "Prepared")), source.Clock.Now);
        var fact = await PolicySeeds.FreezeAsync(source.Content, rules, mutation.Change, operation, 2);
        var after = rules.Authority(((IPolicyFact)fact).Header);
        var concurrent = await source.Policies.RulesAsync(tenant, program, id);
        var other = Uuid.CreateVersion4();
        Assert.Null(concurrent.Review(program, 1, Uuid.CreateVersion4(), "request_changes", "Concurrent",
            ActorReference.ForMember(other, "Other reviewer"), other, source.Clock.Now));
        await source.SeedAsync(concurrent);
        var current = await source.Reader.HydrateAsync(new Policy(tenant, id));
        var physical = current.CommittedStreamPosition;

        // Act
        var result = current.Append(fact, preparedOwner.NativeSequence, preparedOwner.CommittedStreamPosition, mutation, before, after);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Equal(1, current.Revision);
        Assert.Equal(2, current.NativeSequence);
        Assert.Equal(physical, current.CommittedStreamPosition);
        Assert.Equal(2, (await source.FactsAsync(tenant, id)).Count);
    }

    [Fact]
    public async Task ShouldRejectAuthoredPolicyIdGivenIdentifierIdentityMismatch()
    {
        // Arrange
        await using var source = new PolicySourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var authored = Uuid.CreateVersion4();
        var request = new CreatePolicyDraft(authored, tenant, program, "NORMALIZED-ID", Content("Exact identity"));

        // Act
        var result = await new CreatePolicyDraftHandler(source.Operations()).HandleAsync(
            PolicySourceFixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var owner = await source.Reader.HydrateAsync(new Policy(tenant, authored));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.False(owner.IsCreated);
        Assert.Empty(await source.FactsAsync(tenant, authored));
    }

    static PolicyContent Content(string title) => new(title, "Govern access", PolicyAudience.CoreSecurity,
        null, 12, "Policy body", null, "Security owner", []);

    sealed class UnreadableSchema(IImmutableContentStore inner, string schema, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string contentSchema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, contentSchema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("Policy content is temporarily absent.")
                : new ImmutableContentIntegrityException("Policy content digest differs.");
        }
    }
}
