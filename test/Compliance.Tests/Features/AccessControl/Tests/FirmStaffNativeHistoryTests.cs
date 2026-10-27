using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
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
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class FirmStaffNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateRawPrefixGivenCapturedNativeStaffCut(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false, "Original intermediate status");
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, true, "Unchanged latest status");
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var facts = await FactsAsync(host, owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (FirmStaffChangeRecorded)facts[^2];
            var changed = fault == "payload" ? original with { Reason = "A different intermediate status" } : original with { };
            changed.AttachMetadata(fault == "event_id" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
            facts[^2] = changed;
        }

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await FirmStaffHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal(3UL, (await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId))).Revision);
    }

    [Fact]
    public async Task ShouldRejectUnselectedDuplicateLogicalStatusGivenInstalledDefaultReader()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false, "First status");
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var original = owner.Latest!;
        var duplicate = original with
        {
            Revision = owner.Revision + 1,
            Operation = original.Operation with { OriginalExpectedRevision = owner.Revision },
        };
        duplicate.AttachMetadata(original.Metadata with
        {
            EventId = Uuid.CreateVersion4(),
            AggregateVersion = owner.Version + 1,
        });
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(owner.Stream,
            owner.CommittedStreamPosition, [duplicate], CancellationToken.None);

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Contains("repeated logical operation", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPreserveCapturedEarlierStaffCutGivenLaterValidStatusAppend()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        var captured = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false);

        // Act
        var history = await FirmStaffHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), captured);

        // Assert
        Assert.Single(history.Changes);
        Assert.True(history.Matches(captured.Latest!));
        Assert.Equal(1UL, captured.Revision);
        Assert.Equal(2UL, (await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId))).Revision);
    }

    [Fact]
    public async Task ShouldRejectForgedRegistrationCheckpointGivenActualCommittedNativeSource()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var original = owner.Accepted!;
        var forged = original with { Intent = original.Intent with { Sha256 = new string('f', 64) } };
        forged.AttachMetadata(original.Metadata);
        var services = host.Scope.ServiceProvider;

        // Act
        var failure = await Record.ExceptionAsync(() => FirmStaffSource.VerifyAsync(
            services.GetRequiredService<ISourceFactResolver>(), host.Content, owner.Stream, 0, forged, CancellationToken.None));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
        Assert.Equal(1UL, owner.Revision);
        Assert.Equal(owner.Id, original.Metadata.AggregateId);
        Assert.NotEqual(staff.StaffMemberId, owner.Id);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, FirmStaffRecord owner)
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
