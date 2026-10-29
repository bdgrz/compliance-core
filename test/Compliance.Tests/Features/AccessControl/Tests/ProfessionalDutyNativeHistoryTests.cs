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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ProfessionalDutyNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateMiddlePrefixGivenActualHydratedDesignationCut(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var state = await SeedAsync(host);
        var facts = await FactsAsync(host, state.Owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else if (fault == "event_id")
        {
            var original = (FirmProfessionalDutyDesignationRecorded)facts[3];
            var changed = original with { };
            changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
            facts[3] = changed;
        }
        else
        {
            var admission = (ProfessionalDutyChangeAccepted)facts[2];
            var terminal = (FirmProfessionalDutyDesignationRecorded)facts[3];
            var different = admission.Intent with { ContentId = Uuid.CreateVersion4() };
            var admitted = admission with { Intent = different };
            admitted.AttachMetadata(admission.Metadata);
            var completed = terminal with { Intent = different };
            completed.AttachMetadata(terminal.Metadata);
            facts[2] = admitted;
            facts[3] = completed;
        }

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ProfessionalDutyHistory.ReadAsync(new Records(state.Owner.Stream, facts), state.Owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3UL, state.Owner.Revision);
        Assert.Equal(3UL, (await host.HydrateAsync(new FirmProfessionalDutyDesignation(state.User, FirmProfessionalDuty.RuleRatifier, null))).Revision);
    }

    [Fact]
    public async Task ShouldSelectEarlierOriginalReceiptGivenLaterSignedDesignationRefreshes()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var state = await SeedAsync(host);

        // Act
        var selected = await host.HydrateAsync(new FirmProfessionalDutyDesignation(state.User, FirmProfessionalDuty.RuleRatifier,
            null, state.First, state.First));
        var history = await ProfessionalDutyHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), selected);

        // Assert
        Assert.Equal(3UL, selected.Revision);
        Assert.Equal(history.AcceptedFor(state.First), selected.AcceptedFor(state.First));
        Assert.Equal(history.TerminalFor(state.First), selected.TerminalFor(state.First));
        Assert.NotNull(selected.Acceptance.SelectedFact);
        Assert.Equal(6, history.Facts.Count);
        Assert.NotEqual(Assert.IsAssignableFrom<DomainEvent>(selected.Latest).Metadata.EventId,
            selected.TerminalFor(state.First)!.Metadata.EventId);
    }

    static async Task<Seed> SeedAsync(IndependenceHost host)
    {
        var staff = await host.RegisterStaffAsync();
        var designation = Uuid.CreateVersion4();
        Uuid? previous = null;
        Uuid first = default;
        for (var index = 1; index <= 3; index++)
        {
            var signed = ProfessionalDutyAssertionTestIssuer.Issue(designation, staff.UserId, FirmProfessionalDuty.RuleRatifier,
                null, index, "active", previous, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));
            Assert.True((await ProfessionalDutyFacts.RecordAsync(host.Provider, signed, staff, staff.Actor, DateTimeOffset.UtcNow)).IsSuccess);
            first = index == 1 ? signed.Evidence.AssertionId : first;
            previous = signed.Evidence.AssertionId;
        }
        return new Seed(staff.UserId, first,
            await host.HydrateAsync(new FirmProfessionalDutyDesignation(staff.UserId, FirmProfessionalDuty.RuleRatifier, null)));
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, FirmProfessionalDutyDesignation owner)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0))
            found.Add(record.Event);
        return found.ToArray();
    }

    sealed record Seed(Uuid User, Uuid First, FirmProfessionalDutyDesignation Owner);

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
