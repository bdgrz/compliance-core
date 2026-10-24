using System.Security.Cryptography;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates;

public sealed class TemplateApplicationNativeHistoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldKeepCompactFramesAndCompleteHistoryGivenSeventySuccessorDecisions()
    {
        // Arrange
        await using var fixture = new Fixture();
        Assert.True((await fixture.ApplyAsync(fixture.Version(1))).IsSuccess);

        // Act
        for (var number = 2; number <= 71; number++)
        {
            fixture.Scenario.Clock.Now = Now.AddMinutes(number * 2);
            var proposal = new ProposeTemplateSuccessorAdoption(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
                fixture.Template, Fixture.Key, (number - 2) * 2 + 1, number, "adapt");
            var proposed = await fixture.ProposeAsync(proposal, fixture.Version(number));
            Assert.True(proposed.IsSuccess, proposed.Error?.Message);
            fixture.Scenario.Clock.Now = Now.AddMinutes(number * 2 + 1);
            var decision = new RecordTemplateSuccessorDisposition(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
                fixture.Template, Fixture.Key, proposal.ExpectedRevision + 1, number, "adapt", "Retain our exact local procedure.");
            var decided = await fixture.DecideAsync(decision);
            Assert.True(decided.IsSuccess, decided.Error?.Message);
        }
        var snapshot = await fixture.Views.ReadAsync(fixture.Tenant, fixture.Program, fixture.Application);
        var facts = await fixture.FactsAsync();

        // Assert
        Assert.Equal(141, snapshot.Owner.Revision);
        Assert.Equal(70, snapshot.View!.SuccessorProposals.Count);
        Assert.Equal(70, snapshot.View.SuccessorDecisions.Count);
        Assert.Equal(1, snapshot.View.TemplateVersionNumber);
        Assert.False(snapshot.View.IsActivated);
        Assert.Equal(141, facts.Count);
        Assert.All(facts, fact => Assert.True(DomainEventPayload.Size(fact) < DomainEventPayload.MaximumBytes));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(snapshot.Owner.Header).Length < 2500);
        foreach (var fact in facts)
        {
            var native = Assert.IsAssignableFrom<ITemplateApplicationFact>(fact);
            var change = await fixture.Scenario.Content.ReadJsonAsync<TemplateApplicationMutationContent>(
                fixture.Tenant.ToString(), native.Content);
            if (fact is not MethodologyTemplateApplied)
                Assert.Null(change.Application);
            Assert.Null(change.Draft);
        }
    }

    [Fact]
    public async Task ShouldFreezeFullLargeApplicationAndOriginalResultGivenLegalContentBeyondEventFrame()
    {
        // Arrange
        await using var fixture = new Fixture();
        var version = fixture.Version(1, large: true);

        // Act
        var applied = await fixture.ApplyAsync(version);
        var snapshot = await fixture.Views.ReadAsync(fixture.Tenant, fixture.Program, fixture.Application);
        var fact = Assert.IsType<MethodologyTemplateApplied>(Assert.Single(await fixture.FactsAsync()));
        var response = await FirstResponses.ReplayAsync(fixture.Scenario.Content, fixture.Tenant.ToString(), fact.FirstResponse);
        var result = await fixture.Scenario.Content.ReadJsonAsync<MutationResultContent>(fixture.Tenant.ToString(), fact.OperationResult);
        var json = JsonSerializer.SerializeToElement(fact, ComplianceCoreJsonContext.Default.MethodologyTemplateApplied);

        // Assert
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        Assert.Equal(100, snapshot.View!.Drafts.Count);
        Assert.Equal(100, snapshot.View.SourceVersion.Content.Records.Count);
        Assert.Equal(version.ContentSha256, snapshot.View.TemplateContentSha256);
        Assert.True(fact.Content.Bytes > DomainEventPayload.MaximumBytes);
        Assert.True(DomainEventPayload.Size(fact) < DomainEventPayload.MaximumBytes);
        Assert.True(json.TryGetProperty("content", out _));
        Assert.True(json.TryGetProperty("header", out _));
        Assert.False(json.TryGetProperty("state", out _));
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(UnitResult.Schema, response.ResultSchema);
        Assert.Equal("mutation-result-content.v1", fact.OperationResult.Schema);
        Assert.Equal(response.ResultSchema, result.Schema);
        Assert.Equal(response.Body, result.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayBeforeApplicationHistoryReadsAndClassifyFreshReadGivenUnreadableContent(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var version = fixture.Version(1);
        Assert.True((await fixture.ApplyAsync(version)).IsSuccess);
        var before = await fixture.Scenario.Reader.HydrateAsync(new TemplateApplication(fixture.Tenant,
            fixture.Program, fixture.Application));
        var missing = new UnreadableSchema(fixture.Scenario.Content, unavailable);
        fixture.Scenario.Clock.Now = Now.AddDays(1);
        var operations = fixture.Scenario.ApplicationOperations(missing);
        var called = false;
        var request = fixture.ApplyRequest;

        // Act
        var renamed = new Cntryl.Portia.RequestContext<ApplyMethodologyTemplate>(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", fixture.User.ToString()),
                new Claim("name", "Renamed actor")], "UnitTest")));
        var replay = await operations.ExecuteAsync(renamed,
            fixture.Tenant, fixture.Program, fixture.Template, Fixture.Key, fixture.Application,
            nameof(ApplyMethodologyTemplate), null, (_, _, _, _, _) =>
            {
                called = true;
                throw new InvalidOperationException("An exact retry must not read or prepare current source material.");
            }, CancellationToken.None);
        var query = await new GetTemplateApplicationHandler(new TemplateApplicationReader(fixture.Scenario.Reader,
            fixture.Scenario.Events, missing)).HandleAsync(MethodologyTemplateScenario.Context(new GetTemplateApplication(
                fixture.Tenant, fixture.Program, fixture.Application), fixture.User), CancellationToken.None);
        var after = await fixture.Scenario.Reader.HydrateAsync(new TemplateApplication(fixture.Tenant,
            fixture.Program, fixture.Application));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.False(called);
        Assert.False(query.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, query.Error!.Kind);
        Assert.Equal(unavailable, query.Error.IsTransient);
        Assert.Equal(1, missing.RejectedReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("gate")]
    [InlineData("intent")]
    [InlineData("kind")]
    public async Task ShouldRejectChangedNativeBindingGivenHistoricalLogicalIdentity(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        Assert.True((await fixture.ApplyAsync(fixture.Version(1))).IsSuccess);
        var request = new ProposeTemplateSuccessorAdoption(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program,
            fixture.Template, Fixture.Key, 1, 2, "adapt");
        Assert.True((await fixture.ProposeAsync(request, fixture.Version(2))).IsSuccess);
        Assert.True((await fixture.DecideAsync(new RecordTemplateSuccessorDisposition(Uuid.CreateVersion4(), fixture.Tenant,
            fixture.Program, fixture.Template, Fixture.Key, 2, 2, "adopt", "Fits our procedure."))).IsSuccess);
        var before = await fixture.FactsAsync();
        var prepareCalled = false;
        var candidate = request with
        {
            ExpectedRevision = changed == "gate" ? 3 : 1,
            Disposition = changed == "intent" ? "decline" : "adapt",
        };

        // Act
        var replay = await fixture.ProposeAsync(request, fixture.Version(2));
        var rejected = await fixture.Scenario.ApplicationOperations().ExecuteAsync(
            MethodologyTemplateScenario.Context(candidate, changed == "actor" ? Uuid.CreateVersion4() : fixture.User),
            fixture.Tenant, fixture.Program, fixture.Template, Fixture.Key, request.CommandId,
            changed == "kind" ? nameof(RecordTemplateSuccessorDisposition) : nameof(ProposeTemplateSuccessorAdoption),
            candidate.ExpectedRevision, (_, _, _, _, _) =>
            {
                prepareCalled = true;
                throw new InvalidOperationException("A changed accepted binding must conflict before preparation.");
            }, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error!.Kind);
        Assert.False(prepareCalled);
        Assert.Equal(before.Count, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldHideForeignProgramAndTenantGivenScopedApplicationRead()
    {
        // Arrange
        await using var fixture = new Fixture();
        Assert.True((await fixture.ApplyAsync(fixture.Version(1))).IsSuccess);
        var handler = new GetTemplateApplicationHandler(fixture.Views);

        // Act
        var own = await handler.HandleAsync(MethodologyTemplateScenario.Context(new GetTemplateApplication(
            fixture.Tenant, fixture.Program, fixture.Application), fixture.User), CancellationToken.None);
        var foreignProgram = await handler.HandleAsync(MethodologyTemplateScenario.Context(new GetTemplateApplication(
            fixture.Tenant, Uuid.CreateVersion4(), fixture.Application), fixture.User), CancellationToken.None);
        var foreignTenant = await handler.HandleAsync(MethodologyTemplateScenario.Context(new GetTemplateApplication(
            Uuid.CreateVersion4(), fixture.Program, fixture.Application), fixture.User), CancellationToken.None);

        // Assert
        Assert.True(own.IsSuccess, own.Error?.Message);
        Assert.Equal(fixture.Template, own.Value!.TemplateId);
        Assert.Equal(RequestErrorKind.NotFound, foreignProgram.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, foreignTenant.Error?.Kind);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("media_type")]
    [InlineData("after_body")]
    public async Task ShouldRejectUnboundPreparedContentGivenNativeApplicationAppend(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        var member = RbacIds.Member(fixture.Tenant, fixture.User);
        var owner = new TemplateApplication(fixture.Tenant, fixture.Program, fixture.Application, fixture.Application);
        var rules = new TemplateApplicationRules(fixture.Tenant, fixture.Program, fixture.Application);
        Assert.Null(rules.Apply(fixture.Version(1), Fixture.Key, member, "Original actor", Now));
        var mutation = Assert.Single(rules.PendingChanges);
        var content = await fixture.Scenario.Content.WriteJsonAsync(fixture.Tenant.ToString(), Uuid.CreateVersion4(),
            TemplateApplicationReader.Schema, mutation);
        var response = await UnitResponses.FreezeAsync(fixture.Scenario.Content, fixture.Tenant.ToString());
        var first = await FirstResponses.FreezeAsync(fixture.Scenario.Content, fixture.Tenant.ToString(), response);
        var result = await fixture.Scenario.Content.WriteJsonAsync(fixture.Tenant.ToString(), Uuid.CreateVersion4(),
            "mutation-result-content.v1", new MutationResultContent(response.ResultSchema, response.Body));
        var operation = OwnerProvenance.For(owner, fixture.Application, nameof(ApplyMethodologyTemplate),
            ActorReference.ForMember(member, member.ToString()), fixture.Application.ToString(), null,
            new string('a', 64), Now);
        var reference = changed switch
        {
            "length" => content with { Bytes = content.Bytes + 1 },
            "media_type" => content with { MediaType = "application/octet-stream" },
            _ => content,
        };
        var fact = new MethodologyTemplateApplied(fixture.Tenant, fixture.Application, 1, member, rules.Header(),
            reference, Now, operation, first, result);
        var after = changed == "after_body" ? rules.Current! with { RequestedByDisplay = "Poisoned attribution" } : rules.Current!;

        // Act
        var appended = owner.Append(fact, 0, 0, mutation, null, after);

        // Assert
        Assert.False(appended.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, appended.Error!.Kind);
        Assert.False(owner.IsCreated);
        Assert.Empty(new AggregateScenario<TemplateApplication>(owner).PendingEvents);
    }

    [Fact]
    public async Task ShouldPreserveOriginalActorDisplayGivenRenamedRequesterRetry()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = fixture.ApplyRequest;
        var original = new Cntryl.Portia.RequestContext<ApplyMethodologyTemplate>(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", fixture.User.ToString()),
                new Claim("email", "original@example.com")], "UnitTest")));
        var renamed = new Cntryl.Portia.RequestContext<ApplyMethodologyTemplate>(request,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "bdgrz"), new Claim("sub", fixture.User.ToString()),
                new Claim("email", "renamed@example.com")], "UnitTest")));
        var version = fixture.Version(1);
        var operations = fixture.Scenario.ApplicationOperations();
        var prepared = 0;

        // Act
        var accepted = await operations.ExecuteAsync(original, fixture.Tenant, fixture.Program, fixture.Template,
            Fixture.Key, fixture.Application, nameof(ApplyMethodologyTemplate), null,
            (rules, actor, display, at, _) =>
            {
                prepared++;
                return ValueTask.FromResult(rules.Apply(version, Fixture.Key, actor, display, at));
            }, CancellationToken.None);
        fixture.Scenario.Clock.Now = Now.AddDays(1);
        var replayed = await operations.ExecuteAsync(renamed, fixture.Tenant, fixture.Program, fixture.Template,
            Fixture.Key, fixture.Application, nameof(ApplyMethodologyTemplate), null,
            (_, _, _, _, _) => throw new InvalidOperationException("An exact replay must not prepare current material."),
            CancellationToken.None);
        var fact = Assert.IsType<MethodologyTemplateApplied>(Assert.Single(await fixture.FactsAsync()));
        var read = await fixture.Views.ReadAsync(fixture.Tenant, fixture.Program, fixture.Application);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(1, prepared);
        Assert.Equal("member", fact.Operation.Actor.Kind);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), fact.Operation.Actor.Id);
        Assert.Equal("original@example.com", fact.Operation.Actor.Display);
        Assert.Equal("original@example.com", fact.Header.ActorDisplay);
        Assert.Equal("original@example.com", read.View!.RequestedByDisplay);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public const string Key = "long-lived-application";
        public MethodologyTemplateScenario Scenario { get; } = new(Now);
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Template { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Application => TemplateApplicationId.For(Program, Template, Key);
        public TemplateApplicationReader Views => Scenario.ApplicationViews;
        public ApplyMethodologyTemplate ApplyRequest => new(Tenant, Program, Template, 1, Key);

        public ValueTask<Result> ApplyAsync(MethodologyTemplateVersionView version) =>
            Scenario.ApplicationOperations().ExecuteAsync(MethodologyTemplateScenario.Context(ApplyRequest, User), Tenant,
                Program, Template, Key, Application, nameof(ApplyMethodologyTemplate), null,
                (rules, actor, display, at, _) => ValueTask.FromResult(rules.Apply(version, Key, actor, display, at)),
                CancellationToken.None);
        public ValueTask<Result> ProposeAsync(ProposeTemplateSuccessorAdoption request, MethodologyTemplateVersionView version) =>
            Scenario.ApplicationOperations().ExecuteAsync(MethodologyTemplateScenario.Context(request, User), Tenant,
                Program, Template, Key, request.CommandId, nameof(ProposeTemplateSuccessorAdoption), request.ExpectedRevision,
                (rules, actor, display, at, _) => ValueTask.FromResult(rules.ProposeSuccessor(request.ExpectedRevision,
                    version, request.Disposition, actor, display, at)), CancellationToken.None);
        public ValueTask<Result> DecideAsync(RecordTemplateSuccessorDisposition request) =>
            new RecordTemplateSuccessorDispositionHandler(Scenario.ApplicationOperations()).HandleAsync(
                MethodologyTemplateScenario.Context(request, User), CancellationToken.None);

        public MethodologyTemplateVersionView Version(long number, bool large = false)
        {
            var content = new MethodologyTemplateContent("SOC 2 starter", "Complete approved template content.", "soc2",
                Enumerable.Range(0, large ? 100 : 1).Select(index => new MethodologyTemplateRecordContent($"control-{index:000}",
                    "control", $"Control {index}", large ? new string('x', 2000) : "Review access quarterly.", [])).ToArray());
            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content))).ToLowerInvariant();
            return new(Template, Uuid.CreateVersion5(Template, $"version:{number}"), number, "published", content, hash,
                Uuid.CreateVersion5(Template, "author"), "Author", Now.AddDays(-3), new MethodologyTemplateReviewView(
                    Uuid.CreateVersion5(Template, $"review:{number}"), "approved", "Reviewed.",
                    Uuid.CreateVersion5(Template, "reviewer"), "Reviewer", Now.AddDays(-2)),
                Uuid.CreateVersion5(Template, "approver"), "Approver", Now.AddDays(-1), "Approved version.", [], "Approved for reuse.");
        }

        public async Task<IReadOnlyList<DomainEvent>> FactsAsync()
        {
            var facts = new List<DomainEvent>();
            await foreach (var record in Scenario.Events.ReadAsync(EventStreamPattern.ForPattern(Tenant.ToString(),
                TemplateApplication.Area, Application.ToString()), EventCursor.Start, CancellationToken.None))
                if (!record.Event.Metadata.IsAudit)
                    facts.Add(record.Event);
            return facts;
        }
        public ValueTask DisposeAsync() => Scenario.DisposeAsync();
    }

    sealed class UnreadableSchema(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public int RejectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != TemplateApplicationReader.Schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            RejectedReads++;
            throw unavailable ? new ImmutableContentUnavailableException("Application content is not visible yet.")
                : new ImmutableContentIntegrityException("Application content differs from its immutable digest.");
        }
    }
}
