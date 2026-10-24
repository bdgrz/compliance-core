using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlNativeContentTests
{
    [Fact]
    public async Task ShouldRetainExactHistoryGivenManyNativeContentRevisions()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-HISTORY");
        await source.SeedProgramAsync(tenant, program, author);
        Assert.True((await new CreateControlDraftHandler(source.Operations(), source.Reader, new ValidApplicability())
            .HandleAsync(ControlSourceFixture.Context(new CreateControlDraft(tenant, program, "NATIVE-HISTORY",
                Content("Revision 1")), author), CancellationToken.None)).IsSuccess);
        var handler = new ReviseControlDraftHandler(source.Operations(), new ValidApplicability());

        // Act
        for (var revision = 2; revision <= 70; revision++)
        {
            source.Clock.Now = source.Clock.Now.AddMinutes(1);
            var result = await handler.HandleAsync(ControlSourceFixture.Context(new ReviseControlDraft(
                Uuid.CreateVersion4(), tenant, program, id, revision - 1, Content($"Revision {revision}")), author),
                CancellationToken.None);
            Assert.True(result.IsSuccess, $"Revision {revision}: {result.Error?.Message}");
        }
        var owner = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));
        var first = await source.Controls.RevisionAsync(tenant, program, id, 1);
        var current = await source.Controls.ReadAsync(tenant, program, id);
        var facts = await source.FactsAsync(tenant, id);

        // Assert
        Assert.Equal(70, owner.NativeSequence);
        Assert.Equal(70, owner.Revision);
        Assert.Equal("Revision 1", first!.Content.Title);
        Assert.Equal("Revision 70", current.CurrentContent!.Title);
        Assert.Equal(70, facts.Count);
        Assert.All(facts, fact => Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!).Length < 8 * 1024));
        Assert.DoesNotContain("Revision 1", JsonSerializer.Serialize(owner.Header,
            ComplianceCoreJsonContext.Default.ControlAuthorityHeader), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayOriginalResponseAndRejectFreshMutationGivenUnreadableControlHistory(bool unavailable)
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-RETRY");
        await source.SeedProgramAsync(tenant, program, author);
        var create = new CreateControlDraft(tenant, program, "NATIVE-RETRY", Content("First"));
        Assert.True((await new CreateControlDraftHandler(source.Operations(), source.Reader, new ValidApplicability())
            .HandleAsync(ControlSourceFixture.Context(create, author), CancellationToken.None)).IsSuccess);
        var revision = new ReviseControlDraft(Uuid.CreateVersion4(), tenant, program, id, 1, Content("Second"));
        var revisions = new ReviseControlDraftHandler(source.Operations(), new ValidApplicability());
        Assert.True((await revisions.HandleAsync(ControlSourceFixture.Context(revision, author), CancellationToken.None)).IsSuccess);
        Assert.True((await revisions.HandleAsync(ControlSourceFixture.Context(new ReviseControlDraft(Uuid.CreateVersion4(),
            tenant, program, id, 2, Content("Third")), author), CancellationToken.None)).IsSuccess);
        var before = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));
        var missing = new UnreadableSchema(source.Content, ControlFactContent.ChangeSchema, unavailable);
        var handler = new ReviseControlDraftHandler(source.Operations(missing), new FailingApplicability());

        // Act
        var replay = await handler.HandleAsync(ControlSourceFixture.Context(revision, author), CancellationToken.None);
        var creation = await new CreateControlDraftHandler(source.Operations(missing), source.Reader,
                new FailingApplicability()).HandleAsync(ControlSourceFixture.Context(create, author), CancellationToken.None);
        var changedActor = await handler.HandleAsync(ControlSourceFixture.Context(revision, Uuid.CreateVersion4()), CancellationToken.None);
        var changedIntent = await handler.HandleAsync(ControlSourceFixture.Context(revision with { Content = Content("Changed") },
            author), CancellationToken.None);
        var changedRevision = await handler.HandleAsync(ControlSourceFixture.Context(revision with { ExpectedRevision = 2 },
            author), CancellationToken.None);
        var fresh = await handler.HandleAsync(ControlSourceFixture.Context(new ReviseControlDraft(Uuid.CreateVersion4(),
            tenant, program, id, 3, Content("Fresh")), author), CancellationToken.None);
        var after = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.True(creation.IsSuccess, creation.Error?.Message);
        Assert.Equal(new ControlRegistration(id, "NATIVE-RETRY", 1), creation.Value);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fresh.Error?.Kind);
        Assert.Equal(unavailable, fresh.Error!.IsTransient);
        Assert.Equal(1, missing.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPublishOriginalResultEnvelopesGivenNoOpAndDecisionNativeCommands()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-RESULTS");
        await source.SeedProgramAsync(tenant, program, author);
        Assert.True((await new CreateControlDraftHandler(source.Operations(), source.Reader, new ValidApplicability())
            .HandleAsync(ControlSourceFixture.Context(new CreateControlDraft(tenant, program, "NATIVE-RESULTS",
                Content("First")), author), CancellationToken.None)).IsSuccess);
        var revisions = new ReviseControlDraftHandler(source.Operations(), new ValidApplicability());
        Assert.True((await revisions.HandleAsync(ControlSourceFixture.Context(new ReviseControlDraft(Uuid.CreateVersion4(),
            tenant, program, id, 1, Content("Second")), author), CancellationToken.None)).IsSuccess);
        var unchanged = new ReviseControlDraft(Uuid.CreateVersion4(), tenant, program, id, 2, Content("Second"));
        Assert.True((await revisions.HandleAsync(ControlSourceFixture.Context(unchanged, author), CancellationToken.None)).IsSuccess);
        var review = new ReviewControl(Uuid.CreateVersion4(), tenant, program, id, 2, "accept", "Reviewed exact content");
        Assert.True((await new ReviewControlHandler(source.Operations(), new ControlActivationReleaseGate(true))
            .HandleAsync(ControlSourceFixture.Context(review, reviewer, http: true), CancellationToken.None)).IsSuccess);
        var before = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));

        // Act
        var replay = await new ReviewControlHandler(source.Operations(), new ControlActivationReleaseGate(false))
            .HandleAsync(ControlSourceFixture.Context(review, reviewer, http: true), CancellationToken.None);
        var facts = await source.FactsAsync(tenant, id);
        var after = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));
        var view = await source.Controls.ReadAsync(tenant, program, id);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(2, view.Revision);
        Assert.Equal(review.CommandId, view.AcceptedReviewDecisionId);
        Assert.Equal(4, facts.Count);
        Assert.Equal(0, Assert.IsType<ControlDraftRevised>(facts[2]).ChangeCount);
        foreach (var fact in facts)
        {
            var native = Assert.IsAssignableFrom<IControlFact>(fact);
            Assert.Equal("mutation-result-content.v1", native.OperationResult!.Schema);
            var original = await source.Content.ReadJsonAsync<MutationResultContent>(tenant.ToString(), native.OperationResult);
            var expectedSchema = fact is ControlDraftCreated ? ControlResponses.Registration : UnitResult.Schema;
            Assert.Equal(expectedSchema, original.Schema);
            var response = await FirstResponses.ReplayAsync(source.Content, tenant.ToString(), native.FirstResponse!);
            Assert.Equal(expectedSchema, response.ResultSchema);
            Assert.Equal(response.Body, original.Value);
            var body = Assert.IsType<StandardResponseBody>(original.Value);
            Assert.Equal(expectedSchema, body.Content.Schema);
            var json = JsonSerializer.SerializeToElement(fact, ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!);
            Assert.True(json.TryGetProperty("change", out _));
            Assert.True(json.TryGetProperty("authority", out _));
            Assert.False(json.TryGetProperty("state", out _));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyScopedReadAndHideForeignProgramGivenUnreadableContent(bool unavailable)
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-SCOPED");
        var rules = new ControlRules(tenant, id);
        Assert.True(rules.Create(program, id, "NATIVE-SCOPED", Content("Scoped"), RbacIds.Member(tenant, author),
            "Original author", source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        var missing = new UnreadableSchema(source.Content, ControlFactContent.DraftSchema, unavailable);
        var reads = new ControlActivationSource(new ControlReader(source.Reader, source.Events, missing));

        // Act
        var foreignProgram = await reads.LoadAsync(tenant, Uuid.CreateVersion4(), id, CancellationToken.None);
        var foreignTenant = await reads.LoadAsync(Uuid.CreateVersion4(), program, id, CancellationToken.None);
        var own = await reads.LoadAsync(tenant, program, id, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, foreignProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, foreignTenant.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, own.Error?.Kind);
        Assert.Equal(unavailable, own.Error!.IsTransient);
        Assert.Equal(1, missing.RejectedReads);
    }

    [Fact]
    public async Task ShouldFreezeExactWaiverProofAndReplayAfterExpiryGivenAuthorizedSelfReview()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, author);
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-WAIVER");
        await source.SeedProgramAsync(tenant, program, author);
        Assert.True((await new CreateControlDraftHandler(source.Operations(), source.Reader, new ValidApplicability())
            .HandleAsync(ControlSourceFixture.Context(new CreateControlDraft(tenant, program, "NATIVE-WAIVER",
                Content("Exact waiver")), author), CancellationToken.None)).IsSuccess);
        var denied = await new ReviewControlHandler(source.Operations(), new ControlActivationReleaseGate(true))
            .HandleAsync(ControlSourceFixture.Context(new ReviewControl(Uuid.CreateVersion4(), tenant, program, id,
                1, "accept", "Self review without waiver"), author, http: true), CancellationToken.None);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        var waiver = new SeparationOfDutiesWaiver(tenant, Uuid.CreateVersion4());
        var scope = new SeparationOfDutiesWaiverScope("control", id, ControlVersionIds.Initial(id), 1,
            SeparationOfDutiesActions.Review);
        Assert.Null(waiver.Record(scope, member, Uuid.CreateVersion4(), "Requester", "Exact independent exception",
            source.Clock.Now.AddMinutes(-2), source.Clock.Now.AddHours(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Independent waiver approver", source.Clock.Now.AddMinutes(-1)));
        await source.Writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var request = new ReviewControl(Uuid.CreateVersion4(), tenant, program, id, 1, "accept",
            "Self review under exact independently approved exception", waiver.Id);
        var handler = new ReviewControlHandler(source.Operations(), new ControlActivationReleaseGate(true));
        Assert.True((await handler.HandleAsync(ControlSourceFixture.Context(request, author, http: true), CancellationToken.None)).IsSuccess);
        var before = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));
        source.Clock.Now = source.Clock.Now.AddDays(1);

        // Act
        var replay = await handler.HandleAsync(ControlSourceFixture.Context(request, author, http: true), CancellationToken.None);
        var expired = await handler.HandleAsync(ControlSourceFixture.Context(request with { CommandId = Uuid.CreateVersion4() },
            author, http: true), CancellationToken.None);
        var fact = Assert.IsType<ControlReviewed>((await source.FactsAsync(tenant, id))[^1]);
        var mutation = await ControlFactContent.ReadChangeAsync(source.Content, fact);
        var proof = Assert.Single(mutation.Waivers!);
        var after = await source.Reader.HydrateAsync(new ControlDraft(tenant, id));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, expired.Error?.Kind);
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
        Assert.Equal(waiver.ApprovedAt, proof.ValidFrom);
        Assert.Equal(waiver.ExpiresAt, proof.ValidUntil);
    }

    [Fact]
    public async Task ShouldRejectChangedNativeAuthorshipGivenVerifiedImmutableMutation()
    {
        // Arrange
        await using var source = new ControlSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var id = ControlDraft.IdFor(tenant, program, "NATIVE-BINDING");
        var rules = new ControlRules(tenant, id);
        Assert.True(rules.Create(program, id, "NATIVE-BINDING", Content("Original"), RbacIds.Member(tenant, author),
            "Original recorded author", source.Clock.Now).IsSuccess);
        await source.SeedAsync(rules);
        var fact = Assert.IsType<ControlDraftCreated>(Assert.Single(await source.FactsAsync(tenant, id)));
        var changed = fact with { ActorDisplay = "Changed display evidence" };

        // Act
        var original = await ControlFactContent.ReadChangeAsync(source.Content, fact);
        var rejected = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(() =>
            ControlFactContent.ReadChangeAsync(source.Content, changed).AsTask());

        // Assert
        Assert.IsType<ControlDraftCreatedContent>(Assert.Single(original.Changes));
        Assert.IsNotType<ImmutableContentUnavailableException>(rejected);
    }

    static ControlDraftContent Content(string title) => new(title, "Review access", "Management reviews access",
        "The security lead reviews access quarterly", ["Dated review record"]);

    sealed class ValidApplicability : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content, CancellationToken ct = default) =>
            ValueTask.FromResult(Result.Success);
    }
    sealed class FailingApplicability : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content, CancellationToken ct = default) =>
            throw new InvalidOperationException("An accepted retry must not revalidate mutable applicability.");
    }
    sealed class UnreadableSchema(IImmutableContentStore inner, string schema, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string contentSchema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, contentSchema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("The Control material is temporarily absent.")
                : new ImmutableContentIntegrityException("The Control material digest differs.");
        }
    }
}
