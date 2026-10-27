using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
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
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PracticeCommitmentNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateHistoricalPrefixGivenCapturedPracticeAuthority(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        for (var index = 0; index < 3; index++)
            await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
                subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);
        var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff, subject));
        var facts = await FactsAsync(host, owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (PracticeIndependenceCommitted)facts[4];
            var changed = original with { };
            if (fault == "event_id")
                changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
            else
            {
                var first = (PracticeIndependenceReserved)facts[3];
                var different = Uuid.CreateVersion4();
                var reserved = first with { EngagementId = different };
                reserved.AttachMetadata(first.Metadata);
                facts[3] = reserved;
                changed = original with { EngagementId = different };
                changed.AttachMetadata(original.Metadata);
            }
            facts[4] = changed;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await PracticeCommitmentHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal("attest", owner.Practice);
    }

    [Fact]
    public async Task ShouldReadOriginalSelectedCommitmentGivenLaterSettledCommitments()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var firstRoot = Uuid.CreateVersion4();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            subject, "attest", Uuid.CreateVersion4(), firstRoot, host.MemberReference, IndependenceHost.At);
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        var key = new ReservationKey(firstRoot, stream, 1, stream, "actual_staff_assignment");
        var captured = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, selectedReservation: key));
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);

        // Act
        var original = await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), captured);
        var current = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, selectedReservation: key));
        var history = await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), current);

        // Assert
        Assert.Single(original.Commitments);
        Assert.Equal(2, history.Commitments.Count);
        Assert.Equal(original.CommitmentOf(key), current.CommitmentOf(key));
        Assert.Equal(firstRoot, current.CommitmentOf(key)!.Operation.RootOperationId);
        Assert.Null(history.CommitmentOf(key with { FirstOwner = new ServiceEngagementIndependence(host.Tenant, Uuid.CreateVersion4()).Stream }));
        Assert.False(current.Uses.HasActiveUse);
        Assert.Equal(2, history.Assignments().Count);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, PracticeIndependenceCommitment owner)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0))
            found.Add(record.Event);
        return found.ToArray();
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
}
