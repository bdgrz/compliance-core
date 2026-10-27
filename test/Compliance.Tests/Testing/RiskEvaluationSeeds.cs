using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
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
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;
/// <summary>Publishes real immutable native evaluation facts for existing business source fixtures.</summary>
static class RiskEvaluationSeeds
{
    public static async ValueTask<RiskEvaluation> WriteAsync(IAggregateWriter writer, IImmutableContentStore content,
        RiskEvaluationRules source, CancellationToken ct = default)
    {
        var owner = new RiskEvaluation(source.TenantId, source.Id);
        var rules = new RiskEvaluationRules(source.TenantId, source.Id);
        await ApplyAsync(owner, rules, source.AppliedChanges, content, ct);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        source.ClearPendingChanges();
        source.SourceNativeSequence = owner.NativeSequence;
        source.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return owner;
    }
    public static async ValueTask<RiskEvaluation> AppendAsync(IAggregateReader reader, IAggregateWriter writer, IDomainEventReader events,
        IImmutableContentStore content, RiskEvaluationRules source, CancellationToken ct = default)
    {
        var snapshot = await new RiskEvaluationReader(reader, events, content).ReadAsync(source.TenantId, source.ProgramId, source.Id, ct);
        await ApplyAsync(snapshot.Owner, snapshot.Rules, source.PendingChanges.ToArray(), content, ct);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System), ct);
        source.ClearPendingChanges();
        source.SourceNativeSequence = snapshot.Owner.NativeSequence;
        source.SourcePhysicalPosition = snapshot.Owner.CommittedStreamPosition;
        return snapshot.Owner;
    }
    public static async ValueTask<RiskEvaluation> OwnerAsync(IImmutableContentStore content, RiskEvaluationRules source,
        CancellationToken ct = default)
    {
        var owner = new RiskEvaluation(source.TenantId, source.Id);
        await ApplyAsync(owner, new RiskEvaluationRules(source.TenantId, source.Id), source.AppliedChanges, content, ct);
        return owner;
    }
    static async ValueTask ApplyAsync(RiskEvaluation owner, RiskEvaluationRules rules, IReadOnlyList<object> changes,
        IImmutableContentStore content, CancellationToken ct)
    {
        foreach (var change in changes)
        {
            var before = rules.ToView();
            rules.ClearPendingChanges();
            rules.RaiseEvent(change);
            var kind = change switch
            {
                RiskAssessmentRecordedBody => nameof(RecordRiskAssessment),
                RiskTreatmentChosenBody => nameof(ChooseRiskTreatment),
                _ => nameof(AcceptRisk),
            };
            var digest = change switch
            {
                RiskAssessmentRecordedBody value => value.AuthoredIntentSha256!,
                RiskTreatmentChosenBody value => value.AuthoredIntentSha256!,
                RiskAcceptedBody value => value.AuthoredIntentSha256!,
                _ => throw new InvalidOperationException("Another evaluation fixture mutation kind."),
            };
            var operation = OwnerProvenance.For(owner, RiskEvaluationContent.LogicalId(change), kind, RiskEvaluationContent.Actor(change),
                owner.Id.ToString(), unchecked((ulong)before.Revision), digest, RiskEvaluationContent.At(change)) with
            { FirstOwner = owner.Stream };
            var frozen = await RiskEvaluationContent.FreezeAsync(content, before, rules, kind, ct);
            var response = change switch
            {
                RiskAssessmentRecordedBody value => await NativeContent.FreezeAsync(content, owner.TenantId, RiskEvaluationContent.AssessmentResultSchema, value.Assessment, ct),
                RiskAcceptedBody value => await NativeContent.FreezeAsync(content, owner.TenantId, RiskEvaluationContent.AcceptanceResultSchema, value.Acceptance, ct),
                _ => await NativeContent.FreezeUnitAsync(content, owner.TenantId, ct),
            };
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response, ct);
            var result = owner.Append(RiskEvaluationContent.Fact(frozen, owner.NativeSequence + 1, operation, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
    }
}
