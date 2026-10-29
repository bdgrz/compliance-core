using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Admits one exact current-control proposal after native replay and preserves its original registration.</summary>
public sealed class ProposeRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, ControlActivationSource controls, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>
{
    public ValueTask<Result<RiskControlTreatmentRegistration>> HandleAsync(IRequestContext<ProposeRiskControlTreatment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return RiskTreatmentAssertionOperations.ExecuteAsync<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>(operations,
            reader, governanceReader, content, clock, context, request.TenantId, request.ProgramId, request.RiskId, request.CommandId,
            request.ExpectedRevision, request.ControlId, request.CommandId, nameof(ProposeRiskControlTreatment),
            async (owner, operation, token) =>
            {
                var gate = owner.PreparePropose(request.ExpectedRevision);
                if (!gate.IsSuccess)
                    return Result<PreparedRiskAssertion>.Failure(gate.Error!);
                if (request.ControlId == Uuid.Empty || request.ControlVersionId == Uuid.Empty || RiskCommands.InvalidText(request.Rationale))
                    return Failure(RequestErrorKind.Validation, "A control treatment requires an approved control version and bounded rationale.");
                var source = await controls.LoadAsync(request.TenantId, request.ProgramId, request.ControlId, token).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<PreparedRiskAssertion>.Failure(source.Error!);
                if (source.Value.ApprovedVersion is not { Status: "approved" } approved || approved.VersionId != request.ControlVersionId)
                    return Failure(RequestErrorKind.Conflict, "A control treatment must name the control's current approved version.");
                var evaluation = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), token).ConfigureAwait(false);
                if (evaluation.Header?.TreatmentKind != "mitigate")
                    return Failure(RequestErrorKind.Conflict, "Choose the mitigate treatment before asserting a control treatment.");
                if (owner.Accepted is { } active)
                {
                    var accepted = await FindingContent.ReadAsync<RiskControlTreatmentView>(content, request.TenantId,
                        active.Proposed.TreatmentContent, token).ConfigureAwait(false);
                    if (accepted.ControlVersionId == request.ControlVersionId)
                        return Failure(RequestErrorKind.Conflict, "An accepted treatment assertion already names this exact control version.");
                }
                var view = new RiskControlTreatmentView(request.CommandId, request.RiskId, request.ControlId, request.ControlVersionId,
                    RiskStates.TreatmentProposed, request.Rationale.Trim(), operation.Actor, operation.AcceptedAt,
                    null, null, null, null, null, null, null, null)
                { Revision = owner.Revision + 1 };
                var blob = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.TreatmentSchema, view, token).ConfigureAwait(false);
                var frozen = await FindingContent.FreezeAsync(content, request.TenantId, RiskContent.ControlTreatmentRegistrationSchema,
                    new RiskControlTreatmentRegistration(request.CommandId, owner.Revision + 1), token).ConfigureAwait(false);
                return Result<PreparedRiskAssertion>.Success(new(frozen.Response, frozen.Result, blob));
            }, (owner, operation, first, frozen) => owner.Propose(operation, frozen.Proposal!,
                Uuid.Parse(operation.Actor.Id, System.Globalization.CultureInfo.InvariantCulture), first, frozen.Result), ct);
    }

    static Result<PreparedRiskAssertion> Failure(RequestErrorKind kind, string message) => Result<PreparedRiskAssertion>.Failure(new RequestError(kind, message));
}
