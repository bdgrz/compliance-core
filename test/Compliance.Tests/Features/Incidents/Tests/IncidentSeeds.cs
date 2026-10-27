using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Incidents.Tests;

/// <summary>Publishes pure fixture rules through the same compact native append and immutable proof seams as production.</summary>
static class IncidentSeeds
{
    public static async ValueTask<ComplianceIncident> WriteAsync(IAggregateWriter writer, IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var owner = await OwnerAsync(content, source);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        source.ClearPendingChanges();
        return owner;
    }
    public static async ValueTask<ComplianceIncident> OwnerAsync(IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var owner = new ComplianceIncident(source.TenantId, source.ProgramId, source.Id);
        var cursor = new ComplianceIncidentRules(source.TenantId, source.ProgramId, source.Id);
        await AppendAsync(owner, cursor, null, source.AppliedChanges, content);
        return owner;
    }
    public static async ValueTask<ComplianceIncident> AppendAsync(IAggregateWriter writer, ComplianceIncidentReader reader, IImmutableContentStore content, ComplianceIncidentRules source)
    {
        var snapshot = await reader.ReadAsync(source.TenantId, source.ProgramId, source.Id);
        await AppendAsync(snapshot.Owner, snapshot.Rules, snapshot.Authority, source.PendingChanges.ToArray(), content);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System));
        source.ClearPendingChanges();
        return snapshot.Owner;
    }
    static async ValueTask AppendAsync(ComplianceIncident owner, ComplianceIncidentRules cursor, ComplianceIncidentCurrentAuthority? prior,
        IReadOnlyList<object> changes, IImmutableContentStore content)
    {
        foreach (var value in changes)
        {
            var before = cursor.Current;
            cursor.ClearPendingChanges();
            cursor.RaiseEvent(value);
            var state = cursor.Current!;
            var kind = value switch
            {
                ComplianceIncidentRecordedBody => nameof(RecordComplianceIncident),
                ComplianceIncidentRevisedBody => nameof(ReviseComplianceIncident),
                ComplianceIncidentImpactAssessedBody => nameof(AssessComplianceIncident),
                ComplianceIncidentSystemDescriptionDecisionRecordedBody => nameof(RecordComplianceIncidentSystemDescriptionDecision),
                IncidentResolutionRecordedBody => nameof(RecordComplianceIncidentResolution),
                _ => throw new InvalidOperationException("Another incident fixture mutation kind."),
            };
            var command = before is null ? owner.Id : Uuid.CreateVersion5(owner.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var actor = kind == nameof(RecordComplianceIncidentSystemDescriptionDecision)
                ? ActorReference.ForSystemProcess("system", "system") : ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay);
            var op = OwnerProvenance.For(owner, command, kind, actor, owner.Id.ToString(), before is null || kind == nameof(RecordComplianceIncidentSystemDescriptionDecision)
                ? null : unchecked((ulong)before.Revision), AuthoredIntentDigest.Compute(state), state.UpdatedAt) with
            { FirstOwner = owner.Stream };
            var frozen = await ComplianceIncidentContentStore.FreezeAsync(content, before, prior, owner.Header, cursor, kind, CancellationToken.None);
            var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId);
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response);
            var result = owner.Append(ComplianceIncidentContentStore.Fact(frozen, owner.NativeSequence + 1, op, first, response.Result), frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
            prior = frozen.Authority;
        }
    }
}
