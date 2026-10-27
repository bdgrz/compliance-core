using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
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
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProductClientServiceMetadataCommitmentTests
{
    [Theory]
    [InlineData("correlation", false)]
    [InlineData("causation", false)]
    [InlineData("execution", false)]
    [InlineData("actor", false)]
    [InlineData("correlation", true)]
    [InlineData("causation", true)]
    [InlineData("execution", true)]
    [InlineData("actor", true)]
    public async Task ShouldRejectDifferentUnselectedSavedMetadataGivenCapturedServicePrefix(string field, bool execute)
    {
        // Arrange
        await using var original = new ProductClientServiceFixture();
        var tenant = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var rules = new ProductClientServiceRules(tenant, service);
        var member = Uuid.CreateVersion4();
        var at = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.True(rules.Create(Uuid.CreateVersion4(), "Payroll", "Process payroll", "Operations", member, "Author", at).IsSuccess);
        Assert.Null(rules.Revise(1, "Second", "Purpose", "Owner", member, "Author", at.AddMinutes(1)));
        Assert.Null(rules.Revise(2, "Third", "Purpose", "Owner", member, "Author", at.AddMinutes(2)));
        await original.PublishAsync(rules);
        var history = await original.Services.HistoryAsync(tenant, service);
        var facts = history.Facts.ToArray();
        var second = Assert.IsType<ClientServiceRevised>(facts[1]);
        var changed = second with { };
        changed.AttachMetadata(field switch
        {
            "correlation" => second.Metadata with { CorrelationId = Uuid.CreateVersion4() },
            "causation" => second.Metadata with { CausationId = Uuid.CreateVersion4() },
            "execution" => second.Metadata with { ExecutionId = Uuid.CreateVersion4() },
            _ => second.Metadata with { Actor = new ActorAttribution("another-worker", "bdgrz.system") },
        });
        Assert.Equal(second.Metadata.EventId, changed.Metadata.EventId);
        Assert.Equal(second.Metadata.AggregateVersion, changed.Metadata.AggregateVersion);
        facts[1] = changed;
        await using var fixture = new ProductClientServiceFixture(original.Events, new Records(history.Owner.Stream, facts));
        var reached = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (execute)
                _ = await fixture.Executor.ExecuteAsync(new ClientService(tenant, service), _ =>
                {
                    reached = true;
                    return AggregateOutcome.Discard(Result.Success);
                }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            else
                _ = await fixture.Reader.HydrateAsync(new ClientService(tenant, service));
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(reached);
    }

    sealed class Records(EventStreamAddress stream, IReadOnlyList<DomainEvent> facts) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress selected, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (selected != stream)
                yield break;
            for (var index = checked((int)fromOffset); index < facts.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], (ulong)index, EventCursor.Start);
            }
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var record in ReadAsync(stream, 0, ct).ConfigureAwait(false))
                yield return record;
        }
    }
}
