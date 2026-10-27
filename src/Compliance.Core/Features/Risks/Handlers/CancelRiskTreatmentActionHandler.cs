using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>
///     Records an attributable cancellation of unfinished risk treatment work. Cancellation and completion submission
///     serialize on the action: of two racing writes at the same revision exactly one is accepted, and work whose
///     completion is under review cannot be canceled until the review settles.
/// </summary>
public sealed class CancelRiskTreatmentActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelRiskTreatmentAction>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CancelRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a command id, an action id and a non-negative expected revision."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "CancelRiskTreatmentAction", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;

        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
                () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                    request.CommandId),
                action => action.Acceptance,
                async (action, token) =>
                {
                    try
                    {
                        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
                            request.RiskId, token).ConfigureAwait(false);
                        if (!risk.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(risk.Error!);
                        var prepared = action.PrepareCancel(request.ExpectedRevision);
                        if (!prepared.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                        if (RiskCommands.InvalidText(request.Rationale))
                            return Result<FirstResponseDescriptor>.Failure(CommandFailure.InvalidContent(
                                "Cancelling a treatment action requires a cancellation ID and a rationale of at most 4000 characters.")
                                .ToRequestError());
                        (response, result) = await RiskCommands.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                        return Result<FirstResponseDescriptor>.Success(response!);
                    }
                    catch (ImmutableContentIntegrityException failure)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact treatment action source content cannot be verified.",
                            failure is ImmutableContentUnavailableException));
                    }
                },
                (RiskTreatmentAction action, ImmutableContentRef frozen) => action.Cancel(provenance,
                    request.Rationale.Trim(), actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
            return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact treatment action content cannot be verified.", failure is ImmutableContentUnavailableException));
        }
    }
}
