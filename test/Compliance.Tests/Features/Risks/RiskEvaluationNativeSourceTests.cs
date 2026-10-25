using Bdgrz.Compliance.Features.UserIdentities;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskEvaluationNativeSourceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyExactImmutableFailureGivenEvaluationRead(bool unavailable)
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = RiskDraft.IdFor(tenant, program, "R-NATIVE");
        var actor = Uuid.CreateVersion4();
        var draft = new RiskDraftRules(tenant, risk);
        Assert.True(draft.Create(program, risk, "R-NATIVE", new RiskDraftContent("Risk", "Scenario", "Effect", null), actor, "Author", source.Clock.Now).IsSuccess);
        await RiskDraftSeeds.WriteAsync(source.Writer, source.Content, draft);
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Lead"), source.Clock.Now));
        var rules = new RiskEvaluationRules(tenant, risk);
        Assert.Null(rules.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Original assessment", actor, "Assessor", source.Clock.Now));
        await source.SeedAsync(rules);
        var evaluations = new RiskEvaluationReader(source.Reader, source.Events, new UnreadableContent(source.Content, unavailable));
        var directory = new FitzRiskEvaluationDirectory(source.Kv, evaluations);
        var consistency = new RiskEvaluationReadConsistency(directory, source.Reader, evaluations);

        // Act
        var result = await consistency.GetAsync(tenant, program, risk, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error?.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectAnotherExactOwnerGivenNativeBodyScopeForgery()
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Lead"), source.Clock.Now));
        var rules = new RiskEvaluationRules(tenant, risk);
        Assert.Null(rules.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Original assessment", actor, "Assessor", source.Clock.Now));
        var before = new RiskEvaluationRules(tenant, risk).ToView();
        var frozen = await RiskEvaluationContent.FreezeAsync(source.Content, before, rules, nameof(RecordRiskAssessment));
        var original = Assert.IsType<RiskAssessmentRecordedBody>(frozen.Mutation);
        var bad = frozen with { Mutation = original with { RiskId = Uuid.CreateVersion4() } };
        var owner = new RiskEvaluation(tenant, risk);
        var operation = Bdgrz.Compliance.Features.NativeOperations.OwnerProvenance.For(owner, original.Assessment.AssessmentId,
            nameof(RecordRiskAssessment), original.Assessment.Assessor, risk.ToString(), 0, original.AuthoredIntentSha256!, source.Clock.Now)
            with
        { FirstOwner = owner.Stream };
        var response = await Bdgrz.Compliance.Features.NativeOperations.NativeContent.FreezeAsync(source.Content, tenant,
            RiskEvaluationContent.AssessmentResultSchema, original.Assessment);
        var first = await Bdgrz.Compliance.Features.NativeOperations.FirstResponses.FreezeAsync(source.Content, tenant.ToString(), response.Response);
        var fact = RiskEvaluationContent.Fact(bad, 1, operation, first, response.Result);

        // Act
        var result = owner.Append(fact, bad, 0, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, owner.Revision);
        Assert.Null(owner.Header);
    }

    [Theory]
    [InlineData("first_owner")]
    [InlineData("metadata_owner")]
    [InlineData("metadata_event")]
    [InlineData("header")]
    public async Task ShouldRollBackCurrentAndHistoryGivenAnotherExactEvaluationProjectionSource(string change)
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var (tenant, program, risk, facts) = await HistoryAsync(source);
        var directory = new FitzRiskEvaluationDirectory(source.Kv, source.Evaluations);
        var identity = new CheckpointIdentity(FitzRiskEvaluationDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), RiskEvaluation.Area));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(facts[0]);
            await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
                await directory.ApplyAsync(Alter(facts[1], change)));
        }
        var current = await directory.GetAsync(tenant, risk);
        var history = await directory.ListHistoryAsync(tenant, risk, 10, null);
        var checkpoint = await directory.LoadCheckpointAsync(identity);

        // Assert
        Assert.Null(current);
        Assert.Empty(history.Items);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(program, (await source.Evaluations.ReadAsync(tenant, program, risk)).ProgramId);
    }

    [Theory]
    [InlineData("first_owner")]
    [InlineData("metadata_owner")]
    [InlineData("metadata_version")]
    [InlineData("null_header")]
    [InlineData("missing_metadata")]
    public async Task ShouldRejectMalformedNativeIdentityGivenEvaluationHistoryReader(string change)
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var (tenant, program, risk, _) = await HistoryAsync(source);
        var reader = new RiskEvaluationReader(source.Reader,
            new AlteredEvents(source.Events, value => value is IRiskEvaluationFact { Revision: 1 } ? Alter(value, change) : value), source.Content);

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.ReadAsync(tenant, program, risk));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(RiskEvaluationReadFailure.Of((ImmutableContentIntegrityException)failure!).IsTransient);
    }

    [Fact]
    public async Task ShouldClassifyMissingNestedAssessmentGivenExactImmutableEvaluationBody()
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var (tenant, _, _, facts) = await HistoryAsync(source);
        var original = Assert.IsType<RiskAssessmentRecorded>(facts[0]);
        var body = Assert.IsType<RiskAssessmentRecordedBody>(await RiskEvaluationContent.ReadMutationAsync(source.Content, original));
        var reference = await NativeContent.WriteAsync(source.Content, tenant, RiskEvaluationContent.AssessmentSchema,
            body with { Assessment = null! });
        var malformed = original with { ChangeContent = reference };

        // Act
        var failure = await Record.ExceptionAsync(async () => await RiskEvaluationContent.ReadMutationAsync(source.Content, malformed));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(RiskEvaluationReadFailure.Of((ImmutableContentIntegrityException)failure!).IsTransient);
    }

    [Fact]
    public async Task ShouldKeepNativeAuthorityCutGivenDurableIndependentAuditSession()
    {
        // Arrange
        await using var source = new RiskEvaluationSourceFixture();
        var (tenant, program, risk, _) = await HistoryAsync(source);
        var before = await source.Evaluations.ReadAsync(tenant, program, risk);
        var probe = await source.Reader.HydrateAsync(new AuditProbe(tenant, risk));

        // Act
        probe.RecordAudit();
        await source.Writer.SaveAsync(probe, new RequestDispatchContext(RequestActor.System));
        var records = new List<DomainEventRecord>();
        await foreach (var record in source.Events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(), RiskEvaluation.Area), EventCursor.Start, CancellationToken.None))
            records.Add(record);
        var after = await source.Evaluations.ReadAsync(tenant, program, risk);

        // Assert
        var audit = Assert.Single(records, record => record.Event.Metadata.IsAudit);
        Assert.NotEqual(before.Stream, audit.Stream);
        Assert.Equal(before.Owner.Version, audit.Event.Metadata.AggregateVersion);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Owner.NativeSequence, after.Owner.NativeSequence);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
        Assert.Equal(2, after.Facts.Count);
        Assert.Equal(3, records.Count);
    }

    sealed class AuditProbe : Aggregate
    {
        public AuditProbe(Uuid tenant, Uuid risk) : base(risk, new EventStreamAddress(tenant.ToString(), RiskEvaluation.Area, risk.ToString()))
        {
            On<RiskAssessmentRecorded>(_ => { });
            On<RiskTreatmentChosen>(_ => { });
        }
        public void RecordAudit() => AuditEvent(new UserIdentityAuthenticated(Id, "native-risk-test", "audit-session"));
    }

    static async Task<(Uuid Tenant, Uuid Program, Uuid Risk, IReadOnlyList<DomainEvent> Facts)> HistoryAsync(RiskEvaluationSourceFixture source)
    {
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(actor, "Lead"), source.Clock.Now));
        var rules = new RiskEvaluationRules(tenant, risk);
        Assert.Null(rules.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
            "Original assessment", actor, "Assessor", source.Clock.Now));
        Assert.Null(rules.ChooseTreatment(program, 1, "accept", "Chosen treatment", actor, "Assessor", source.Clock.Now));
        await source.SeedAsync(rules);
        return (tenant, program, risk, await source.FactsAsync(tenant, risk));
    }

    static DomainEvent Alter(DomainEvent original, string change)
    {
        var value = (DomainEvent)JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(original,
            original.GetType(), ComplianceCoreJsonContext.Default), original.GetType(), ComplianceCoreJsonContext.Default)!;
        var fact = (IRiskEvaluationFact)value;
        var operation = fact.Operation with { FirstOwner = new EventStreamAddress(Uuid.CreateVersion4().ToString(), RiskEvaluation.Area, fact.RiskId.ToString()) };
        var header = change == "null_header" ? null! : fact.Header with { ViewSha256 = new string('a', 64) };
        if (change is "first_owner" or "header" or "null_header")
            value = value switch
            {
                RiskAssessmentRecorded item => item with
                {
                    Operation = change == "first_owner" ? operation : item.Operation,
                    Header = change == "first_owner" ? item.Header : header
                },
                RiskTreatmentChosen item => item with
                {
                    Operation = change == "first_owner" ? operation : item.Operation,
                    Header = change == "first_owner" ? item.Header : header
                },
                _ => throw new InvalidOperationException("Unexpected native test source kind."),
            };
        if (change == "missing_metadata")
            return value;
        var metadata = original.Metadata;
        value.AttachMetadata(metadata with
        {
            AggregateId = change == "metadata_owner" ? Uuid.CreateVersion4() : metadata.AggregateId,
            EventId = change == "metadata_event" ? Uuid.CreateVersion4() : metadata.EventId,
            AggregateVersion = change == "metadata_version" ? metadata.AggregateVersion + 1 : metadata.AggregateVersion,
        });
        return value;
    }

    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEvent, DomainEvent> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset = 0,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, offset, ct))
                yield return record with { Event = alter(record.Event) };
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor = default,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return record with { Event = alter(record.Event) };
        }
    }

    sealed class UnreadableContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            Task.FromException<ReadOnlyMemory<byte>>(unavailable ? new ImmutableContentUnavailableException("Native evaluation source is not visible.")
                : new ImmutableContentIntegrityException("Native evaluation source is corrupt."));
    }
}
