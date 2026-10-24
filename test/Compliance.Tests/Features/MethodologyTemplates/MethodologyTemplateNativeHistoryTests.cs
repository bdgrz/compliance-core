using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class MethodologyTemplateNativeHistoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRetainEveryHistoricalVersionGivenMoreThanFormerLifetimeLimit()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        Assert.True((await scenario.CreateAsync(new CreateMethodologyTemplate(template, Content("version-1"),
            "Initial"), author)).IsSuccess);

        // Act
        for (var number = 2; number <= 66; number++)
        {
            scenario.Clock.Now = Now.AddMinutes(number);
            var revised = await scenario.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(),
                template, number - 1, Content($"version-{number}"), $"Revision {number}"), author);
            Assert.True(revised.IsSuccess, $"Version {number}: {revised.Error?.Message}");
        }
        var owner = await scenario.Reader.HydrateAsync(new MethodologyTemplate(template));
        var view = await scenario.Views.ReadAsync(template);
        var facts = await scenario.FactsAsync(template);

        // Assert
        Assert.Equal(66, owner.Revision);
        Assert.Equal(66, owner.VersionNumber);
        Assert.NotNull(owner.CurrentContent);
        Assert.Equal(66, view!.Versions.Count);
        Assert.Equal(Enumerable.Range(1, 66).Select(number => $"version-{number}"),
            view.Versions.Select(version => version.Content.Records[0].Summary));
        Assert.All(view.Versions.Take(65), version => Assert.Equal("superseded", version.Status));
        Assert.Equal("draft", view.Versions[^1].Status);
        foreach (var fact in facts.Cast<IMethodologyTemplateFact>())
        {
            var delta = await scenario.Content.ReadJsonAsync<MethodologyTemplateView>(MethodologyTemplate.Realm,
                fact.Change);
            Assert.Single(delta.Versions);
        }
    }

    [Fact]
    public async Task ShouldPersistBodyOutsideNativeFactsGivenAuthoredTemplateLargerThanEventFrame()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var content = Content("placeholder") with
        {
            Records = Enumerable.Range(1, 80).Select(number => new MethodologyTemplateRecordContent(
                $"control-{number}", "control", $"Original control {number}", new string('x', 1200), [])).ToArray(),
        };
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(content).Length > 48 * 1024);

        // Act
        var result = await scenario.CreateAsync(new CreateMethodologyTemplate(template, content,
            "Large original body"), Uuid.CreateVersion4());
        var fact = Assert.Single(await scenario.FactsAsync(template));
        var view = await scenario.Views.ReadAsync(template);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var json = JsonSerializer.SerializeToElement(fact, fact.GetType());
        Assert.False(json.TryGetProperty("State", out _));
        Assert.DoesNotContain(new string('x', 1200), json.GetRawText());
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact, fact.GetType()).Length < 48 * 1024);
        Assert.Equal(80, Assert.Single(view!.Versions).Content.Records.Count);
    }

    [Fact]
    public async Task ShouldReplayAllOriginalResultsGivenHistoricalBodiesUnavailableAfterRetirement()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var create = new CreateMethodologyTemplate(template, Content("first"), "Create");
        var revise = new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), template, 1, Content("second"), "Revise");
        var review = new ReviewMethodologyTemplate(Uuid.CreateVersion4(), template, 2, "approved", "Review");
        var publish = new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(), template, 2, "Publish");
        var retire = new RetireMethodologyTemplate(Uuid.CreateVersion4(), template, 4,
            new DateOnly(2026, 11, 1), "Retire");
        Assert.True((await scenario.CreateAsync(create, author)).IsSuccess);
        Assert.True((await scenario.ReviseAsync(revise, author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(review, reviewer)).IsSuccess);
        Assert.True((await scenario.PublishAsync(publish, approver)).IsSuccess);
        Assert.True((await new RetireMethodologyTemplateHandler(scenario.Operations()).HandleAsync(
            MethodologyTemplateScenario.Context(retire, approver), CancellationToken.None)).IsSuccess);
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplate(template));
        var missing = new UnreadableSchema(scenario.Content, MethodologyTemplateReader.Schema, unavailable: true);
        await Assert.ThrowsAsync<ImmutableContentUnavailableException>(() =>
            new MethodologyTemplateReader(scenario.Reader, scenario.Events, missing).ReadAsync(template).AsTask());
        scenario.Clock.Now = Now.AddDays(1);
        var operations = scenario.Operations(missing);

        // Act
        var results = new[]
        {
            await new CreateMethodologyTemplateHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(create, author), CancellationToken.None),
            await new ReviseMethodologyTemplateDraftHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(revise, author), CancellationToken.None),
            await new ReviewMethodologyTemplateHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(review, reviewer), CancellationToken.None),
            await new ApproveMethodologyTemplateVersionHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(publish, approver), CancellationToken.None),
            await new RetireMethodologyTemplateHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(retire, approver), CancellationToken.None),
        };
        var changed = await new ReviseMethodologyTemplateDraftHandler(operations).HandleAsync(
            MethodologyTemplateScenario.Context(revise with { ExpectedRevision = 4 }, author), CancellationToken.None);
        var after = await scenario.Reader.HydrateAsync(new MethodologyTemplate(template));

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(1, missing.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("history", true)]
    [InlineData("history", false)]
    [InlineData("response", true)]
    [InlineData("response", false)]
    [InlineData("result", true)]
    [InlineData("result", false)]
    public async Task ShouldPreserveAuthorityAndAllowRecoveryGivenUnreadableImmutableTemplateMaterial(string stage,
        bool unavailable)
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var create = new CreateMethodologyTemplate(template, Content("first"), "Initial");
        var revise = new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), template, 1, Content("second"), "Revise");
        Assert.True((await scenario.CreateAsync(create, author)).IsSuccess);
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplate(template));
        var schema = stage switch
        {
            "history" => MethodologyTemplateReader.Schema,
            "response" => FirstResponses.Schema,
            _ => UnitResult.Schema,
        };
        var operations = scenario.Operations(new UnreadableSchema(scenario.Content, schema, unavailable));

        // Act
        var result = stage == "history"
            ? await new ReviseMethodologyTemplateDraftHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(revise, author), CancellationToken.None)
            : await new CreateMethodologyTemplateHandler(operations).HandleAsync(
                MethodologyTemplateScenario.Context(create, author), CancellationToken.None);
        var afterFailure = await scenario.Reader.HydrateAsync(new MethodologyTemplate(template));
        var recovered = stage == "history" ? await scenario.ReviseAsync(revise, author)
            : await scenario.CreateAsync(create, author);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, afterFailure.CommittedStreamPosition);
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
    }

    [Fact]
    public async Task ShouldRetainExactPublishedPredecessorGivenSuccessorReviewAndTemplateRetirement()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        Assert.True((await scenario.CreateAsync(new CreateMethodologyTemplate(template, Content("first"),
            "Initial"), author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), template,
            1, "approved", "Reviewed"), reviewer)).IsSuccess);
        Assert.True((await scenario.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            template, 1, "Publish"), approver)).IsSuccess);
        var original = await scenario.Views.VersionAsync(template, 1);
        Assert.True((await scenario.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), template,
            3, Content("successor"), "Revise"), author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), template,
            2, "changes_requested", "Return successor"), reviewer)).IsSuccess);
        Assert.True((await new RetireMethodologyTemplateHandler(scenario.Operations()).HandleAsync(
            MethodologyTemplateScenario.Context(new RetireMethodologyTemplate(Uuid.CreateVersion4(), template,
                5, new DateOnly(2026, 11, 1), "Retire old platform methodology"), approver), CancellationToken.None)).IsSuccess);

        // Act
        var predecessor = await scenario.Views.VersionAsync(template, 1);
        var view = await scenario.Views.ReadAsync(template);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(predecessor));
        Assert.Equal("published", predecessor!.Status);
        Assert.Equal("changes_requested", view!.Versions[^1].Status);
        Assert.Equal("retired", view.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayApplicationWithoutHistoryAndClassifyNewSourceReadGivenUnreadableTemplate(bool unavailable)
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var client = Uuid.CreateVersion4();
        var program = new ComplianceProgram(tenant, programId);
        Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Lead", Now));
        await scenario.Writer.SaveAsync(program, new RequestDispatchContext(RequestActor.System));
        Assert.True((await scenario.CreateAsync(new CreateMethodologyTemplate(template, Content("first"),
            "Initial"), author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), template,
            1, "approved", "Reviewed"), reviewer)).IsSuccess);
        Assert.True((await scenario.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            template, 1, "Publish"), approver)).IsSuccess);
        var request = new ApplyMethodologyTemplate(tenant, programId, template, 1, "annual-review");
        Assert.True((await new ApplyMethodologyTemplateHandler(scenario.ApplicationOperations(), scenario.Views).HandleAsync(MethodologyTemplateScenario.Context(request, client), CancellationToken.None)).IsSuccess);
        var applicationId = TemplateApplicationId.For(programId, template, request.ApplicationKey);
        var before = await scenario.Reader.HydrateAsync(new TemplateApplication(tenant, programId, applicationId));
        var missing = new UnreadableSchema(scenario.Content, MethodologyTemplateReader.Schema, unavailable);
        var handler = new ApplyMethodologyTemplateHandler(scenario.ApplicationOperations(),
            new MethodologyTemplateReader(scenario.Reader, scenario.Events, missing));
        scenario.Clock.Now = Now.AddDays(1);

        // Act
        var replay = await handler.HandleAsync(MethodologyTemplateScenario.Context(request, client), CancellationToken.None);
        var changed = await handler.HandleAsync(MethodologyTemplateScenario.Context(request with { TemplateVersion = 2 },
            client), CancellationToken.None);
        var fresh = await handler.HandleAsync(MethodologyTemplateScenario.Context(request with { ApplicationKey = "other" },
            client), CancellationToken.None);
        var after = await scenario.Reader.HydrateAsync(new TemplateApplication(tenant, programId, applicationId));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fresh.Error?.Kind);
        Assert.Equal(unavailable, fresh.Error!.IsTransient);
        Assert.Equal(1, missing.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPublishMutationResultEnvelopeGivenEveryNativeTemplateCommand()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateScenario(Now);
        var template = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        Assert.True((await scenario.CreateAsync(new CreateMethodologyTemplate(template, Content("first"), "Create"), author)).IsSuccess);
        Assert.True((await scenario.ReviseAsync(new ReviseMethodologyTemplateDraft(Uuid.CreateVersion4(), template,
            1, Content("second"), "Revise"), author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(new ReviewMethodologyTemplate(Uuid.CreateVersion4(), template,
            2, "approved", "Review"), reviewer)).IsSuccess);
        Assert.True((await scenario.PublishAsync(new ApproveMethodologyTemplateVersion(Uuid.CreateVersion4(),
            template, 2, "Publish"), approver)).IsSuccess);
        Assert.True((await new RetireMethodologyTemplateHandler(scenario.Operations()).HandleAsync(
            MethodologyTemplateScenario.Context(new RetireMethodologyTemplate(Uuid.CreateVersion4(), template,
                4, new DateOnly(2026, 11, 1), "Retire"), approver), CancellationToken.None)).IsSuccess);

        // Act
        var facts = await scenario.FactsAsync(template);

        // Assert
        Assert.Equal(5, facts.Count);
        foreach (var fact in facts)
        {
            var native = Assert.IsAssignableFrom<IMethodologyTemplateFact>(fact);
            Assert.Equal("mutation-result-content.v1", native.OperationResult!.Schema);
            var result = await scenario.Content.ReadJsonAsync<MutationResultContent>(MethodologyTemplate.Realm,
                native.OperationResult);
            Assert.Equal(UnitResult.Schema, result.Schema);
            var body = Assert.IsType<StandardResponseBody>(result.Value);
            Assert.Equal(UnitResult.Schema, body.Content.Schema);
            _ = await scenario.Content.ReadJsonAsync<UnitResult>(MethodologyTemplate.Realm, body.Content);
            var json = JsonSerializer.SerializeToElement(fact,
                ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!);
            Assert.True(json.TryGetProperty("change", out _));
            Assert.False(json.TryGetProperty("state", out _));
            Assert.False(json.TryGetProperty("content", out _));
        }
    }

    static MethodologyTemplateContent Content(string summary) => new("SOC 2 methodology",
        "Original methodology content", "soc2",
        [new MethodologyTemplateRecordContent("access-review", "control", "Review privileged access", summary, [])]);

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
            throw unavailable ? new ImmutableContentUnavailableException("The immutable template material is temporarily absent.")
                : new ImmutableContentIntegrityException("The immutable template material digest differs.");
        }
    }
}
