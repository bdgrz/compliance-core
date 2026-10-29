using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorRequestReplayCutTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectContradictoryRawPrefixGivenActualHydratedRequestCut(string fault)
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var state = await SeedAsync(fixture);
        var facts = await AlternateAsync(fixture, state, fault);
        var split = new Records(state.Owner.Stream, facts);

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await AuditorRequestHistory.ReadAsync(split, state.Owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3, state.Owner.Revision);
        Assert.Equal(3, (await fixture.RequestAsync(state.Record.RequestId)).Revision);
    }

    [Fact]
    public async Task ShouldRejectCoherentAlternatePreparedPrefixGivenSameNativeVersionTailAndLatestFact()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var state = await SeedAsync(fixture);
        var facts = await AlternateAsync(fixture, state, "payload");
        var events = new InMemoryEventStore();
        await events.AppendAsync(state.Owner.Stream, 0, facts, CancellationToken.None);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddPortia();
        services.AddVerifiedAuditorRequestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var alternateReader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var alternate = await alternateReader.HydrateAsync(new AuditorRequest(fixture.TenantId, fixture.ProgramId,
            state.Record.RequestId));
        var native = new NativeOperationExecutor(fixture.Reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), fixture.Content);
        var handler = ActivatorUtilities.CreateInstance<ReviseAuditorRequestHandler>(fixture.Services, native);
        var command = new ReviseAuditorRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            state.Record.RequestId, 3, "Revised wording", "Exact source scope", "normal",
            new DateOnly(2026, 11, 15), "The append must use its actual prepared native prefix.");

        // Act
        var result = await handler.HandleAsync(fixture.Context(command), CancellationToken.None);
        var after = await alternateReader.HydrateAsync(new AuditorRequest(fixture.TenantId, fixture.ProgramId,
            state.Record.RequestId));

        // Assert
        Assert.Equal(state.Owner.Version, alternate.Version);
        Assert.Equal(state.Owner.CommittedStreamPosition, alternate.CommittedStreamPosition);
        Assert.Equal(state.Owner.Latest, alternate.Latest);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(3, after.Revision);
        Assert.Equal(3, (await fixture.RequestAsync(state.Record.RequestId)).Revision);
    }

    [Fact]
    public async Task ShouldPreserveCapturedEarlierCutGivenLegitimateLaterCommentAppend()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var state = await SeedAsync(fixture);
        var captured = state.Owner;
        var later = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            state.Record.RequestId, "A legitimate later append.", "internal", captured.Revision);
        Assert.True((await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(later),
            CancellationToken.None)).IsSuccess);

        // Act
        var history = await AuditorRequestHistory.ReadAsync(fixture.Events, captured);
        var current = await fixture.RequestAsync(state.Record.RequestId);

        // Assert
        Assert.Equal(3, history.Facts.Count);
        Assert.Equal(2, history.Comments.Count);
        Assert.Equal(3, captured.Revision);
        Assert.Equal(4, current.Revision);
    }

    static async Task<Seed> SeedAsync(AuditorRequestFixture fixture)
    {
        await fixture.SeedMemberAsync(fixture.UserId);
        var record = fixture.NewRecordCommand();
        Assert.True((await fixture.Handler<RecordAuditorRequestHandler>().HandleAsync(fixture.Context(record),
            CancellationToken.None)).IsSuccess);
        var middle = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            record.RequestId, "The original intermediate contribution.", "internal", 1);
        Assert.True((await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(middle),
            CancellationToken.None)).IsSuccess);
        var last = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            record.RequestId, "An unchanged final native fact.", "internal", 2);
        Assert.True((await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(last),
            CancellationToken.None)).IsSuccess);
        var owner = await fixture.RequestAsync(record.RequestId);
        var facts = new List<DomainEvent>();
        await foreach (var item in fixture.Events.ReadAsync(owner.Stream, 0, CancellationToken.None))
            facts.Add(item.Event);
        return new Seed(record, middle, owner, facts.ToArray());
    }

    static async Task<DomainEvent[]> AlternateAsync(AuditorRequestFixture fixture, Seed state, string fault)
    {
        var facts = state.Facts.ToArray();
        if (fault == "truncate")
            return facts[..^1];
        var original = (AuditorCommentAdmitted)facts[1];
        var changed = original with { };
        if (fault == "event_id")
            changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
        else
        {
            var command = state.Middle with { Body = "A different coherent intermediate contribution." };
            var operation = original.Operation with { AuthoredIntentSha256 = AuditorCommands.Digest(command) };
            var old = await fixture.ReadAsync<ChildAdmissionIntent>(original.Intent);
            var authored = await AuditorContent.WriteAsync(fixture.Content, fixture.TenantId, AuditorContent.CommentSchema,
                new AuditorCommentContent(fixture.ProgramId, state.Record.RequestId, command.CommentId, command.Body,
                    command.Compartment, fixture.MemberId, operation.Actor.Display, operation.AcceptedAt), CancellationToken.None);
            var intent = await AuditorContent.WriteAsync(fixture.Content, fixture.TenantId, ChildAdmissionIntent.Schema,
                old with { Header = AuditorContent.Header(operation, operation.AcceptedAt), CanonicalAuthoredContent = authored },
                CancellationToken.None);
            changed = original with { Intent = intent, Operation = operation };
            changed.AttachMetadata(original.Metadata);
        }
        facts[1] = changed;
        return facts;
    }

    sealed record Seed(RecordAuditorRequest Record, RecordAuditorRequestComment Middle, AuditorRequest Owner,
        DomainEvent[] Facts);

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
}
