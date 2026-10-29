using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

public sealed class AdvisorNativeHistoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectUnselectedDuplicateOperationGivenDefaultOrExecutorHydration(bool thread, bool execute)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        var command = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            handoffId, feedbackId, "First response.", 1);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(command), CancellationToken.None)).IsSuccess);
        var parent = await host.HandoffAsync(handoffId);
        var child = await host.ThreadAsync(handoffId, feedbackId);
        DomainEvent duplicate;
        if (thread)
        {
            var first = Assert.Single(child.Facts.OfType<AdvisorFeedbackResponded>());
            duplicate = first with { Operation = first.Operation with { OriginalExpectedRevision = 2 } };
        }
        else
        {
            var first = Assert.Single(parent.Admitted);
            duplicate = first with { Revision = 4, Operation = first.Operation with { OriginalExpectedRevision = 3 } };
        }
        var stream = thread ? child.Stream : parent.Stream;
        var version = thread ? 2UL : 3UL;
        DomainEventSeed.Attach(duplicate, thread ? child.Id : parent.Id, version + 1);
        await host.Services.GetRequiredService<IEventStore>().AppendAsync(stream, version, [duplicate]);
        var entered = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (thread)
            {
                var owner = new AdvisorFeedbackThread(host.TenantId, host.ProgramId, handoffId, feedbackId);
                if (execute)
                    await host.Services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await host.Reader.HydrateAsync(owner);
            }
            else
            {
                var owner = new AdvisorHandoff(host.TenantId, host.ProgramId, handoffId);
                if (execute)
                    await host.Services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await host.Reader.HydrateAsync(owner);
            }
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(entered);
        Assert.Equal(version + 1, (ulong)(await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectAcceptedRetryGivenMalformedLaterUnselectedHistory(bool thread)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, selected) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        var admission = host.Feedback(handoffId, feedbackId, selected, 2);
        Assert.True((await host.RecordAsync(admission)).IsSuccess);
        await host.MaterializeAsync(handoffId);
        var response = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            handoffId, feedbackId, "Accepted response.", 1);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(response), CancellationToken.None)).IsSuccess);
        DomainEvent malformed;
        EventStreamAddress stream;
        Uuid ownerId;
        ulong version;
        if (thread)
        {
            var child = await host.ThreadAsync(handoffId, feedbackId);
            var original = Assert.Single(child.Facts.OfType<AdvisorFeedbackResponded>());
            // The selected original response remains unique; a different root carries another owner's scope.
            malformed = original with
            {
                ProgramId = Uuid.CreateVersion4(),
                Operation = original.Operation with
                { LogicalId = Uuid.CreateVersion4(), RootOperationId = Uuid.CreateVersion4(), OriginalExpectedRevision = 2 }
            };
            (stream, ownerId, version) = (child.Stream, child.Id, 2);
        }
        else
        {
            var parent = await host.HandoffAsync(handoffId);
            var original = Assert.Single(parent.Admitted);
            var other = Uuid.CreateVersion4();
            malformed = original with
            {
                Revision = 4,
                Generation = 99,
                Operation = original.Operation with
                { LogicalId = other, RootOperationId = other, OriginalExpectedRevision = 3 }
            };
            (stream, ownerId, version) = (parent.Stream, parent.Id, 3);
        }
        DomainEventSeed.Attach(malformed, ownerId, version + 1);
        await host.Services.GetRequiredService<IEventStore>().AppendAsync(stream, version, [malformed]);

        // Act
        var error = thread
            ? (await host.RespondHandler.HandleAsync(host.Http(response), CancellationToken.None)).Error
            : (await host.RecordAsync(admission)).Error;

        // Assert
        Assert.NotNull(error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.False(error.IsTransient);
        Assert.Equal(version + 1, (ulong)(await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectSubstitutionOrTruncationGivenActualHydratedCut(bool truncate)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        for (var index = 0; index < 2; index++)
            Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
                Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoff, feedback, "Response " + index, index + 1)),
                CancellationToken.None)).IsSuccess);
        var snapshot = await host.ThreadAsync(handoff, feedback);
        var facts = snapshot.Facts.ToArray();
        if (truncate)
            facts = facts[..^1];
        else
        {
            var original = (AdvisorFeedbackResponded)facts[1];
            var modified = original with { Authored = ((AdvisorFeedbackResponded)facts[2]).Authored };
            modified.AttachMetadata(original.Metadata);
            facts[1] = modified;
        }
        var reader = new AdvisorAggregateReader(host.Reader, new Records(snapshot.Stream, facts));

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.HydrateAsync(
            new AdvisorFeedbackThread(host.TenantId, host.ProgramId, handoff, feedback)));

        // Assert
        if (truncate)
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3, snapshot.Revision);
        Assert.Equal(3, (await host.EventsAsync<DomainEvent>(snapshot.Stream)).Count);
    }

    [Fact]
    public async Task ShouldReplayWithoutHistoricalTextReadGivenResolutionRevocationAndCurrentPersonalAuthorization()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var request = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            handoff, feedback, "The original response.", 1);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(request), CancellationToken.None)).IsSuccess);
        Assert.True((await host.ResolveHandler.HandleAsync(host.Http(new ResolveAdvisorFeedback(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, handoff, feedback, 2)), CancellationToken.None)).IsSuccess);
        Assert.True((await host.EndAsync(handoff, 3, revoke: true)).IsSuccess);
        var fault = new HistoricalTextUnavailable(host.Content);
        var native = new NativeOperationExecutor(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(), fault);
        var handler = new RespondToAdvisorFeedbackHandler(new AdvisorUnitOperation(native, fault), host.Threads, fault, host.Clock);

        // Act
        var replay = await handler.HandleAsync(host.Http(request), CancellationToken.None);
        var changed = await handler.HandleAsync(host.Http(request with { Response = "Changed response." }), CancellationToken.None);
        var forbidden = await handler.HandleAsync(AdvisorHost.System(request), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, forbidden.Error?.Kind);
        Assert.Equal(0, fault.BlockedReads);
        Assert.Equal(3, (await host.EventsAsync<DomainEvent>(new AdvisorFeedbackThread(host.TenantId,
            host.ProgramId, handoff, feedback).Stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectCoherentAlternateCutGivenDifferentPreparationAndRealExecutorReader()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var first = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            handoff, feedback, "Prepared prefix.", 1);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(first), CancellationToken.None)).IsSuccess);
        var original = await host.ThreadAsync(handoff, feedback);
        var alternative = first with { Response = "A different coherent prefix." };
        var oldFact = Assert.Single(original.Facts.OfType<AdvisorFeedbackResponded>());
        var reference = await AdvisorAuthored.WriteAsync(host.Content, host.TenantId, alternative, CancellationToken.None);
        var substituted = oldFact with
        {
            Authored = reference,
            Operation = oldFact.Operation with
            { AuthoredIntentSha256 = AuthoredIntentDigest.Compute(alternative) }
        };
        substituted.AttachMetadata(oldFact.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(original.Stream, 0, [original.Facts[0], substituted]);
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        var descriptor = services.Last(value => !value.IsKeyedService && value.ServiceType == typeof(IAggregateReader));
        var key = new object();
        services.Remove(descriptor);
        if (descriptor.ImplementationFactory is { } factory)
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key, (provider, _) => factory(provider), descriptor.Lifetime));
        else
            services.Add(ServiceDescriptor.DescribeKeyed(typeof(IAggregateReader), key, descriptor.ImplementationType!, descriptor.Lifetime));
        services.Add(ServiceDescriptor.Describe(typeof(IAggregateReader), provider =>
            new AdvisorAggregateReader(provider.GetRequiredKeyedService<IAggregateReader>(key), events), descriptor.Lifetime));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var alternateReader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var alternate = await new AdvisorFeedbackReader(alternateReader, events, host.Content).ReadAsync(host.TenantId,
            host.ProgramId, handoff, feedback);
        var native = new NativeOperationExecutor(host.Reader, scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), host.Content);
        var handler = new RespondToAdvisorFeedbackHandler(new AdvisorUnitOperation(native, host.Content), host.Threads,
            host.Content, host.Clock);
        var request = new RespondToAdvisorFeedback(Uuid.CreateVersion4(), host.TenantId, host.ProgramId,
            handoff, feedback, "Must not use a different prepared authority.", 2);

        // Act
        var result = await handler.HandleAsync(host.Http(request), CancellationToken.None);
        var unchanged = await alternateReader.HydrateAsync(new AdvisorFeedbackThread(host.TenantId,
            host.ProgramId, handoff, feedback));

        // Assert
        Assert.Equal(original.Version, alternate.Version);
        Assert.Equal(original.Owner.CommittedStreamPosition, alternate.Owner.CommittedStreamPosition);
        Assert.Equal(alternative.Response, Assert.Single(alternate.Responses).Response);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(2, unchanged.Revision);
        Assert.Equal(2, (await host.EventsAsync<DomainEvent>(original.Stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRollbackRowsAndCheckpointGivenSubstitutedCompleteAuthoredBody(bool thread)
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        var correction = new CorrectAdvisorFeedback(host.TenantId, host.ProgramId, handoff, feedback,
            Uuid.CreateVersion4(), "Corrected contribution.", "Original reason.", 1);
        Assert.True((await host.CorrectHandler.HandleAsync(host.Http(correction), CancellationToken.None)).IsSuccess);
        var handoffDirectory = new FitzAdvisorHandoffDirectory(new InMemoryKvClient(), host.Content);
        var threadDirectory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), host.Content);
        IProjectionStore directory = thread ? threadDirectory : handoffDirectory;
        DomainEvent malformed;
        DomainEvent? before = null;
        if (thread)
        {
            var snapshot = await host.ThreadAsync(handoff, feedback);
            var original = Assert.Single(snapshot.Facts.OfType<AdvisorFeedbackCorrected>());
            var reference = await AdvisorAuthored.WriteAsync(host.Content, host.TenantId,
                correction with { CorrectedBody = "Substituted complete contribution." }, CancellationToken.None);
            malformed = original with { Authored = reference };
            malformed.AttachMetadata(original.Metadata);
            before = snapshot.Recorded!.Fact;
        }
        else
        {
            var original = Assert.Single(await host.EventsAsync<AdvisorHandoffCreated>(new AdvisorHandoff(host.TenantId,
                host.ProgramId, handoff).Stream));
            var request = await AdvisorAuthored.ReadAsync(host.Content, original, CancellationToken.None);
            var reference = await AdvisorAuthored.WriteAsync(host.Content, host.TenantId,
                request with { Purpose = "Substituted complete purpose." }, CancellationToken.None);
            malformed = original with { Authored = reference };
            malformed.AttachMetadata(original.Metadata);
        }
        var name = thread ? FitzAdvisorFeedbackWorkItemDirectory.ProjectorName : FitzAdvisorHandoffDirectory.ProjectorName;
        var area = thread ? AdvisorFeedbackThread.Area : AdvisorHandoff.Area;
        var identity = new CheckpointIdentity(name, EventStreamPattern.ForPattern(host.TenantId.ToString(), area));
        Exception? failure;

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            if (before is not null)
                await threadDirectory.ApplyAsync(before);
            failure = await Record.ExceptionAsync(async () =>
            {
                if (thread)
                    await threadDirectory.ApplyAsync(malformed);
                else
                    await handoffDirectory.ApplyAsync(malformed, (await host.SnapshotAsync(handoff)).Selection);
            });
        }
        var checkpoint = thread ? await threadDirectory.LoadCheckpointAsync(host.TenantId)
            : await handoffDirectory.LoadCheckpointAsync(host.TenantId);
        var work = await threadDirectory.LoadProgramAsync(host.TenantId, host.ProgramId);
        Assert.True(work.IsSuccess, work.Error?.Message);
        var rows = thread ? work.Value.Count
            : (await handoffDirectory.ListProgramAsync(host.TenantId, host.ProgramId, 10, null)).Items.Count;

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(ProjectionCheckpoint.Start, checkpoint);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task ShouldPreserveCompleteTextGivenAllCompactAuthoredNativeFacts()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var text = new string('界', 16000);
        var handoff = Uuid.CreateVersion4();
        var source = AdvisorHost.NewSource();
        var feedback = Uuid.CreateVersion4();

        // Act
        Assert.True((await host.CreateAsync(handoff, [source], text)).IsSuccess);
        Assert.True((await host.ReadyAsync(handoff, 1)).IsSuccess);
        Assert.True((await host.RecordAsync(host.Feedback(handoff, feedback, source, 2, text))).IsSuccess);
        await host.MaterializeAsync(handoff);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, handoff, feedback, text, 1)), CancellationToken.None)).IsSuccess);
        Assert.True((await host.ReviseAsync(handoff, 3, [AdvisorHost.NewSource()], text)).IsSuccess);
        Assert.True((await host.CloseHandler.HandleAsync(host.Http(new CloseAdvisorHandoff(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, handoff, text, 4)), CancellationToken.None)).IsSuccess);
        var second = await host.SeedReadyAsync();
        Assert.True((await host.RevokeHandler.HandleAsync(host.Http(new RevokeAdvisorHandoff(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, second.HandoffId, text, 2)), CancellationToken.None)).IsSuccess);
        var snapshot = await host.HandoffAsync(handoff);
        var thread = await host.ThreadAsync(handoff, feedback);
        var revoked = await host.HandoffAsync(second.HandoffId);
        var native = snapshot.Facts.Concat(thread.Facts).Concat(revoked.Facts).ToArray();

        // Assert
        Assert.Equal(text, snapshot.Purpose);
        Assert.Equal(text, thread.Recorded!.Body);
        Assert.Equal(text, Assert.Single(thread.Responses).Response);
        Assert.Equal(text, (await AdvisorAuthored.ReadAsync(host.Content,
            Assert.Single(snapshot.Facts.OfType<AdvisorHandoffSelectionRevised>()), CancellationToken.None)).ChangeReason);
        Assert.Equal(text, (await AdvisorAuthored.ReadAsync(host.Content,
            Assert.Single(snapshot.Facts.OfType<AdvisorHandoffClosed>()), CancellationToken.None)).Rationale);
        Assert.Equal(text, (await AdvisorAuthored.ReadAsync(host.Content,
            Assert.Single(revoked.Facts.OfType<AdvisorHandoffRevoked>()), CancellationToken.None)).Rationale);
        Assert.All(native, fact => Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!).Length <= 48 * 1024));
    }

    [Fact]
    public async Task ShouldRetainLaterCorrectionsAndResponsesGivenResolvedThreadAndRevokedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoff, feedback, _) = await host.SeedFeedbackAsync();
        Assert.True((await host.ResolveHandler.HandleAsync(host.Http(new ResolveAdvisorFeedback(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, handoff, feedback, 1)), CancellationToken.None)).IsSuccess);
        Assert.True((await host.EndAsync(handoff, 3, revoke: true)).IsSuccess);

        // Act
        var corrected = await host.CorrectHandler.HandleAsync(host.Http(new CorrectAdvisorFeedback(host.TenantId,
            host.ProgramId, handoff, feedback, Uuid.CreateVersion4(), "Later complete correction.", "Clarified after resolution.", 2)),
            CancellationToken.None);
        var responded = await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(Uuid.CreateVersion4(),
            host.TenantId, host.ProgramId, handoff, feedback, "Later complete response.", 3)), CancellationToken.None);
        var snapshot = await host.ThreadAsync(handoff, feedback);

        // Assert
        Assert.True(corrected.IsSuccess, corrected.Error?.Message);
        Assert.True(responded.IsSuccess, responded.Error?.Message);
        Assert.Equal(4, snapshot.Revision);
        Assert.Equal("Please clarify the control owner.", snapshot.Recorded!.Body);
        Assert.NotNull(snapshot.Resolved);
        Assert.Equal("Later complete correction.", Assert.Single(snapshot.Corrections).CorrectedBody);
        Assert.Equal("Later complete response.", Assert.Single(snapshot.Responses).Response);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }

    sealed class HistoricalTextUnavailable(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int BlockedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema.EndsWith(".AuthoredContent.v1", StringComparison.Ordinal) || reference.Schema == ChildAdmissionIntent.Schema)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("Historical authored text is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
