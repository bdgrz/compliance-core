using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessHistoryVerificationTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("missing", true)]
    [InlineData("foreign_stream", false)]
    [InlineData("physical_gap", false)]
    [InlineData("changed_body", false)]
    public async Task ShouldVerifyExactReadinessHistoryGivenUnavailableOrContradictoryCut(string fault, bool transient)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        await ReadinessSeed.AdoptAsync(reader, services.GetRequiredService<IAggregateExecutor>(), content,
            tenant, program, assessment, new ReadinessEvaluation([], [], [], new string('a', 64)), Now,
            Uuid.CreateVersion4(), Now);
        var captured = await reader.HydrateAsync(new ProgramReadiness(tenant, program));
        var events = services.GetRequiredService<IDomainEventReader>();
        var altered = new AlteredEvents(events, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with { Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), record.Stream.Area, record.Stream.Resource) },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "changed_body" when record.Event is ReadinessAssessmentAdopted adopted => record with { Event = Changed(adopted) },
            _ => record,
        });

        // Act
        var failure = await Record.ExceptionAsync(async () => await ReadinessHistory.ReadAsync(altered, captured));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);

        static ReadinessAssessmentAdopted Changed(ReadinessAssessmentAdopted original)
        {
            var changed = original with { SourceProofs = original.Intent };
            changed.AttachMetadata(original.Metadata);
            return changed;
        }
    }

    [Fact]
    public async Task ShouldReadOriginalCutAndReplayHistoricalCompletionGivenLaterAssessment()
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var oldAssessment = Uuid.CreateVersion4();
        var newAssessment = Uuid.CreateVersion4();
        var evaluation = new ReadinessEvaluation([], [], [], new string('a', 64));
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, oldAssessment, evaluation, Now, member, Now);
        var root = await ReadinessSeed.DecideAsync(reader, executor, content, tenant, program, oldAssessment,
            ReadinessDecisionKind.TypeIEntry, ProgramReadiness.Defer, "Review again.", member, Now);
        var captured = await reader.HydrateAsync(new ProgramReadiness(tenant, program, root));
        var admission = Assert.IsType<TypeIEntryDecisionAccepted>(captured.AcceptedDecision(root));
        var terminal = Assert.IsType<TypeIEntryDecisionRecorded>(captured.TerminalDecision(root));
        var command = new CompleteTypeIEntryDecision(terminal.Operation.LogicalId,
            NativeFactReference.Of(captured.Stream, admission), captured.Stream, root, admission.Generation,
            null, admission.Intent, program, oldAssessment, terminal.DecisionProofs);
        await ReadinessSeed.AdoptAsync(reader, executor, content, tenant, program, newAssessment, evaluation, Now, member, Now.AddMinutes(1));
        var before = await reader.HydrateAsync(new ProgramReadiness(tenant, program));

        // Act
        var history = await ReadinessHistory.ReadAsync(events, captured);
        var replayed = await ReadinessProcesses.SendReceiverAsync(provider, command);
        var after = await reader.HydrateAsync(new ProgramReadiness(tenant, program, terminal.Operation.LogicalId, root));
        var composed = await services.GetRequiredService<ReadinessReader>().ReadViewAsync(tenant, program, oldAssessment);

        // Assert
        Assert.Equal(captured.Version, (ulong)history.Count);
        Assert.Equal(terminal.Metadata.EventId, history[^1].Metadata.EventId);
        Assert.Equal(NativeFactReference.Of(captured.Stream, terminal), Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value).Fact);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(newAssessment, after.LatestAssessmentId);
        Assert.Equal(root, composed!.TypeIEntryDecision!.DecisionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyMissingAndCorruptContentGivenReadinessQuery(bool unavailable)
    {
        // Arrange
        await using var provider = ReadinessTestServices.Build();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var assessment = Uuid.CreateVersion4();
        await ReadinessSeed.AdoptAsync(reader, services.GetRequiredService<IAggregateExecutor>(), content,
            tenant, program, assessment, new ReadinessEvaluation([], [], [], new string('a', 64)), Now,
            Uuid.CreateVersion4(), Now);
        var readiness = new ReadinessReader(reader, new FaultingContent(content, unavailable),
            services.GetRequiredService<IDomainEventReader>());
        var handler = new GetReadinessAssessmentHandler(readiness);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetReadinessAssessment>(
            new GetReadinessAssessment(tenant, program, assessment), RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
    }

    sealed class FaultingContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => Task.FromException<ReadOnlyMemory<byte>>(unavailable
                ? new ImmutableContentUnavailableException("The exact content has not arrived.")
                : new ImmutableContentIntegrityException("The exact content is corrupt."));
    }

    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } selected)
                    yield return selected;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } selected)
                    yield return selected;
        }
    }
}
