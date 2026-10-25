using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramNativeHistoryTests
{
    static readonly DateTimeOffset At = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectUnselectedDuplicateLogicalRootGivenDefaultReadOrExecutor(bool execute)
    {
        // Arrange
        await using var original = new ProgramNativeFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var rules = Rules(tenant, program);
        Assert.Null(rules.Revise(1, "Revised", Plan(), Uuid.CreateVersion4(), "Reviser", At.AddMinutes(1)));
        await original.PublishAsync(rules);
        var history = await original.Programs.HistoryAsync(tenant, program);
        var creation = Assert.IsType<ProgramCreated>(history.Facts[0]);
        var revision = Assert.IsType<ProgramRevised>(history.Facts[1]);
        var malformed = revision with
        {
            Operation = revision.Operation with
            {
                LogicalId = creation.Operation.LogicalId,
                RootOperationId = creation.Operation.LogicalId,
            }
        };
        malformed.AttachMetadata(revision.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(history.Owner.Stream, 0, [creation, malformed]);
        await using var fixture = new ProgramNativeFixture(events);
        var reached = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (execute)
                _ = await fixture.Executor.ExecuteAsync(new ComplianceProgram(tenant, program), _ =>
                {
                    reached = true;
                    return AggregateOutcome.Discard(Result.Success);
                }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            else
                _ = await fixture.Reader.HydrateAsync(new ComplianceProgram(tenant, program));
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(reached);
    }

    [Fact]
    public async Task ShouldRejectAcceptedCreationRetryGivenMalformedUnselectedRevisionHistory()
    {
        // Arrange
        await using var original = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.Null(rules.Revise(1, "Second", Plan(), Uuid.CreateVersion4(), "Second", At.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", Plan(), Uuid.CreateVersion4(), "Third", At.AddMinutes(2)));
        await original.PublishAsync(rules);
        var history = await original.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var second = Assert.IsType<ProgramRevised>(history.Facts[1]);
        var third = Assert.IsType<ProgramRevised>(history.Facts[2]);
        var malformed = third with
        {
            Operation = third.Operation with
            {
                LogicalId = second.Operation.LogicalId,
                RootOperationId = second.Operation.LogicalId,
            }
        };
        malformed.AttachMetadata(third.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(history.Owner.Stream, 0, [history.Facts[0], second, malformed]);
        await using var fixture = new ProgramNativeFixture(events);
        var request = new CreateProgram(rules.Id, rules.TenantId, "SOC 2", Plan());
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());

        // Act
        var result = await fixture.Operations.CreateAsync(new RequestContext<CreateProgram>(request, actor), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Equal("The program's exact immutable source cannot be verified.", result.Error.Message);
    }

    [Fact]
    public async Task ShouldRetainOriginalResponseGivenCreationReplayAfterRevisionAndUnavailableAuthoredBody()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var create = new CreateProgram(program, tenant, "SOC 2", Plan());
        var first = await fixture.Operations.CreateAsync(new RequestContext<CreateProgram>(create, actor), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var revised = await fixture.Operations.ReviseAsync(new RequestContext<ReviseProgram>(new ReviseProgram(Uuid.CreateVersion4(),
            tenant, program, 1, "Later", Plan()), actor), CancellationToken.None);
        Assert.True(revised.IsSuccess, revised.Error?.Message);
        var store = new UnavailableAuthoredContent(fixture.Content);
        var operations = ProgramSeeds.Operations(fixture.Reader, fixture.Writer, fixture.Events, store);

        // Act
        var replay = await operations.CreateAsync(new RequestContext<CreateProgram>(create with { Name = " SOC 2 " }, actor), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(0, store.AuthoredReads);
        Assert.Equal(2, (await fixture.Programs.HistoryAsync(tenant, program)).Facts.Count);
    }

    [Fact]
    public async Task ShouldSeparateNoChangeAcceptanceGivenCurrentEditionAndNewLogicalIdentity()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var edition = Uuid.CreateVersion4();
        Assert.Null(rules.SelectCriteriaEdition(1, edition, Uuid.CreateVersion4(), "Selector", At.AddMinutes(1)));
        await fixture.PublishAsync(rules);
        var before = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var selected = Assert.IsType<ProgramCriteriaEditionSelected>(before.Facts[1]);
        var request = new SelectProgramCriteriaEdition(Uuid.CreateVersion4(), rules.TenantId, rules.Id, 1, edition);
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());

        // Act
        var accepted = await fixture.Operations.SelectAsync(new RequestContext<SelectProgramCriteriaEdition>(request, actor), CancellationToken.None);
        var replay = await fixture.Operations.SelectAsync(new RequestContext<SelectProgramCriteriaEdition>(request, actor), CancellationToken.None);
        var after = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(2, after.Owner.Revision);
        Assert.Equal(3UL, after.Owner.NativeSequence);
        Assert.Equal(3UL, after.Owner.Version);
        Assert.Equal(3UL, after.Owner.CommittedStreamPosition);
        var noChange = Assert.IsType<ProgramCriteriaEditionSelected>(after.Facts[2]);
        Assert.Equal(0, noChange.ChangeCount);
        Assert.Equal(selected.ChangedAt, noChange.ChangedAt);
        Assert.Equal(selected, after.Owner.CurrentSelection);
        Assert.Equal(before.CriteriaSelectionAt(At.AddMinutes(2)), after.CriteriaSelectionAt(At.AddMinutes(2)));
        Assert.Equal(2, after.Facts.OfType<IProgramFact>().Count(fact => fact.ChangeCount == 1));
    }

    [Fact]
    public async Task ShouldKeepExactUnicodeValueOutsideNativePayloadGivenUnboundedAuthoredContent()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var name = string.Concat(Enumerable.Repeat("审计🦡", 16_000));
        var plan = Plan() with { ReadinessAdvisor = name };
        var request = new CreateProgram(Uuid.CreateVersion4(), Uuid.CreateVersion4(), name, plan);
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());

        // Act
        var result = await fixture.Operations.CreateAsync(new RequestContext<CreateProgram>(request, actor), CancellationToken.None);
        var history = await fixture.Programs.HistoryAsync(request.TenantId, request.ProgramId);
        var fact = Assert.IsType<ProgramCreated>(Assert.Single(history.Facts));
        var value = await ProgramReader.ReadContentAsync(fixture.Content, request.TenantId, fact.Content);
        var native = JsonSerializer.SerializeToUtf8Bytes(fact, ComplianceCoreJsonContext.Default.ProgramCreated);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new ProgramContent(name, plan), value);
        Assert.True(fact.Content.Bytes > 49_152);
        Assert.True(native.Length < 49_152);
        var fields = typeof(ComplianceProgram).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ProgramContent) || field.FieldType == typeof(ProgramPlan));
    }

    [Fact]
    public async Task ShouldChooseTimestampThenBusinessRevisionGivenBackdatedAndEqualTimeSelections()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var earlier = Uuid.CreateVersion4();
        var later = Uuid.CreateVersion4();
        var backdated = Uuid.CreateVersion4();
        Assert.Null(rules.SelectCriteriaEdition(1, earlier, Uuid.CreateVersion4(), "First", At.AddMinutes(10)));
        Assert.Null(rules.SelectCriteriaEdition(2, later, Uuid.CreateVersion4(), "Second", At.AddMinutes(10)));
        Assert.Null(rules.SelectCriteriaEdition(3, backdated, Uuid.CreateVersion4(), "Last", At.AddMinutes(5)));
        await fixture.PublishAsync(rules);

        // Act
        var history = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);

        // Assert
        Assert.Equal(backdated, history.Owner.CriteriaEditionId);
        Assert.Equal(backdated, history.CriteriaSelectionAt(At.AddMinutes(7))?.EditionId);
        Assert.Equal(later, history.CriteriaSelectionAt(At.AddMinutes(10))?.EditionId);
        Assert.Equal(3, history.CriteriaSelectionAt(At.AddMinutes(10))?.Revision);
        Assert.Null(history.CriteriaSelectionAt(At));
    }

    [Fact]
    public async Task ShouldRejectPreparedAppendGivenUnfinalizedHydratedCut()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await fixture.PublishAsync(rules);
        var history = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var logical = Uuid.CreateVersion4();
        var owner = new ComplianceProgram(rules.TenantId, rules.Id, logical);
        new AggregateScenario<ComplianceProgram>(owner).Given(history.Facts.ToArray());
        var change = new ProgramRuleChange(nameof(ReviseProgram), 2, new ProgramContent("Later", Plan()), null,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Reviser"), At.AddMinutes(1));
        var candidate = Assert.IsType<ProgramRevised>(await ProgramSeeds.FreezeAsync(fixture.Content,
            rules.TenantId, rules.Id, change, 2, owner.CurrentContent, logical));

        // Act
        var result = owner.Revise(candidate.Operation, candidate.Content, candidate.FirstResponse!, candidate.OperationResult!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<ComplianceProgram>(owner).PendingEvents);
        Assert.Single((await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id)).Facts);
    }

    [Theory]
    [InlineData("correlation")]
    [InlineData("causation")]
    [InlineData("execution")]
    [InlineData("actor")]
    public async Task ShouldRejectUnselectedIntermediateSavedMetadataChangeGivenFinalizedProgramCut(string fault)
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.Null(rules.Revise(1, "Second", Plan(), Uuid.CreateVersion4(), "Second", At.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", Plan(), Uuid.CreateVersion4(), "Third", At.AddMinutes(2)));
        await fixture.PublishAsync(rules);
        var history = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var facts = history.Facts.ToArray();
        var original = Assert.IsType<ProgramRevised>(facts[1]);
        var altered = original with { };
        altered.AttachMetadata(fault switch
        {
            "correlation" => original.Metadata with { CorrelationId = Uuid.CreateVersion4() },
            "causation" => original.Metadata with { CausationId = Uuid.CreateVersion4() },
            "execution" => original.Metadata with { ExecutionId = Uuid.CreateVersion4() },
            "actor" => original.Metadata with { Actor = new ActorAttribution("another-worker", "bdgrz.system") },
            _ => throw new InvalidOperationException(),
        });
        facts[1] = altered;

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await ProgramHistory.ReadAsync(
            new RawProgramRecords(history.Owner.Stream, facts), history.Owner));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3, history.Owner.Revision);
    }

    [Fact]
    public async Task ShouldRefuseCoherentChangedSavedPrefixGivenDifferentPreparedAndExecutorProgramCuts()
    {
        // Arrange
        await using var original = new ProgramNativeFixture();
        var rules = Rules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.Null(rules.Revise(1, "Second", Plan(), Uuid.CreateVersion4(), "Second", At.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", Plan(), Uuid.CreateVersion4(), "Third", At.AddMinutes(2)));
        await original.PublishAsync(rules);
        var history = await original.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var facts = history.Facts.ToArray();
        var intermediate = Assert.IsType<ProgramRevised>(facts[1]);
        var changed = intermediate with { };
        changed.AttachMetadata(intermediate.Metadata with { CausationId = Uuid.CreateVersion4() });
        facts[1] = changed;
        var differentEvents = new InMemoryEventStore();
        await differentEvents.AppendAsync(history.Owner.Stream, 0, facts);
        await using var actual = new ProgramNativeFixture(differentEvents);
        var operations = new ProgramOperations(new NativeOperationExecutor(original.Reader, actual.Executor, original.Content),
            original.Content, TimeProvider.System);
        var request = new ReviseProgram(Uuid.CreateVersion4(), rules.TenantId, rules.Id, 3, "Fourth", Plan());
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());

        // Act
        var refused = await operations.ReviseAsync(new RequestContext<ReviseProgram>(request, actor), CancellationToken.None);
        var beforeRetry = await actual.Programs.HistoryAsync(rules.TenantId, rules.Id);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error?.IsTransient);
        Assert.Equal(3, beforeRetry.Owner.Revision);
        var fresh = new ProgramOperations(new NativeOperationExecutor(actual.Reader, actual.Executor, original.Content),
            original.Content, TimeProvider.System);
        Assert.True((await fresh.ReviseAsync(new RequestContext<ReviseProgram>(request, actor), CancellationToken.None)).IsSuccess);
        Assert.Equal(4, (await actual.Programs.HistoryAsync(rules.TenantId, rules.Id)).Owner.Revision);
    }

    sealed class RawProgramRecords(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong offset, CancellationToken ct = default) => Enumerate(address, offset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct = default) => throw new NotSupportedException();
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong offset, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = offset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }

    static ProgramRules Rules(Uuid tenant, Uuid program)
    {
        var rules = new ProgramRules(tenant, program);
        Assert.Null(rules.Create("SOC 2", Plan(), Uuid.CreateVersion4(), "Manager", At));
        return rules;
    }
    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    sealed class UnavailableAuthoredContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int AuthoredReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema != ProgramContent.Schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            AuthoredReads++;
            return Task.FromException<ReadOnlyMemory<byte>>(new ImmutableContentUnavailableException("The authored value is unavailable."));
        }
    }
}
