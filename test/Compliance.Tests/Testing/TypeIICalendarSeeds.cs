using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes each pure range decision through its actual immutable native owner cut.</summary>
static class TypeIICalendarSeeds
{
    public static Task<TypeIIObservationCalendar> WriteAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, source, source.AppliedChanges.ToArray(), ct);

    public static Task<TypeIIObservationCalendar> AppendAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, source, source.PendingChanges.ToArray(), ct);

    static async Task<TypeIIObservationCalendar> SaveAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, IReadOnlyList<object> changes, CancellationToken ct)
    {
        var calendars = new TypeIICalendarReader(reader, events, content);
        TypeIIObservationCalendar? owner = null;
        foreach (var change in changes)
        {
            var snapshot = await calendars.ReadAsync(source.TenantId, source.Id, TypeIICalendarContent.RequiredRoots(change), ct: ct);
            var rules = snapshot.Rules;
            var decision = change switch
            {
                TypeIICalendarAuthorizationChange value => rules.Authorize(value.Operation, value.Intent, value.PeriodId,
                    value.Kind, value.NewRange, value.PriorOperationId),
                TypeIICalendarSettlementChange value => rules.Settle(value.Operation, value.Intent, value.PeriodId,
                    value.Settlement, value.TargetOperationId, value.ReleasedOperationId),
                _ => throw new InvalidOperationException("Another calendar fixture mutation kind."),
            };
            Assert.True(decision.IsSuccess, decision.Error?.Message);
            var prepared = await TypeIICalendarContent.FreezeAsync(content, snapshot, rules, ct);
            owner = snapshot.Owner;
            var appended = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(appended.IsSuccess, appended.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        source.ClearPendingChanges();
        return owner ?? await reader.HydrateAsync(new TypeIIObservationCalendar(source.TenantId, source.Id), ct);
    }
}
