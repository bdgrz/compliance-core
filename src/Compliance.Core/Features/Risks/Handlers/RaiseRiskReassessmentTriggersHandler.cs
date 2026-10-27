using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Reactors;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Admits one semantic trigger after verified cut-scoped deduplication; identical accepted commands replay first.</summary>
public sealed class RaiseRiskReassessmentTriggersHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, RiskGovernanceReader history) : IRequestHandler<RaiseRiskReassessmentTriggers>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RaiseRiskReassessmentTriggers> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.CommandId == Uuid.Empty || request.RiskId == Uuid.Empty || request.RaisedAt == default ||
            request.TriggerKind is not (RiskStates.MethodChanged or RiskStates.BoundaryChanged) ||
            string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Length > 4000)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reassessment trigger requires its exact command, risk, kind, source and source time."));
        var triggerId = RiskGovernance.TriggerIdFor(request.ProgramId, request.RiskId, request.TriggerKind, request.SourceReference);
        var workload = request.TriggerKind == RiskStates.MethodChanged ? RiskMethodReassessmentReactor.WorkloadName : BoundaryRiskReassessmentReactor.WorkloadName;
        var stream = new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId).Stream;
        var provenance = FindingContent.Provenance(request.CommandId, nameof(RaiseRiskReassessmentTriggers), stream,
            ActorReference.ForSystemProcess("reactor:" + workload, workload), null, FindingCommands.Digest(request), clock.GetUtcNow())
            with
        { FirstOwner = stream };
        var noChange = new RequestError(RequestErrorKind.Conflict, "The semantic reassessment obligation already exists or no longer applies.");
        ImmutableContentRef? triggerContent = null;
        ImmutableContentRef? result = null;
        long revision = 0;
        ulong physical = 0;
        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<RiskGovernance>(AcceptedOperationBinding.Of(provenance),
                () => new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId, request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (await history.FindTriggerAsync(owner, triggerId, token).ConfigureAwait(false) is not null)
                            return Result<FirstResponseDescriptor>.Failure(noChange);
                        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                        if (!risk.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(risk.Error!);
                        var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), token).ConfigureAwait(false);
                        if (evaluation.Header?.InherentMethodVersionId is not { } inherent || request.MethodVersionId is { } method && inherent == method)
                            return Result<FirstResponseDescriptor>.Failure(noChange);
                        triggerContent = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.TriggerSchema,
                            new RiskReassessmentTriggerView(triggerId, request.RiskId, request.TriggerKind, request.SourceReference, request.RaisedAt), token).ConfigureAwait(false);
                        var frozen = await RiskCommands.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                        result = frozen.Result;
                        revision = owner.Revision;
                        physical = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentIntegrityException error)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact reassessment source cannot be verified.", error is ImmutableContentUnavailableException));
                    }
                }, (owner, first) => owner.RaiseTrigger(provenance, triggerId, triggerContent!, first, result!, revision, physical)), context, ct).ConfigureAwait(false);
            // The executor rechecked exact acceptance/collision before exposing this existing semantic no-op.
            if (!outcome.IsSuccess && ReferenceEquals(outcome.Error, noChange))
                return Result.Success;
            return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact reassessment trigger history cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
