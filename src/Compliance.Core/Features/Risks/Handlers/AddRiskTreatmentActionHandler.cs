using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>
///     Adds accountable treatment work to a risk as its own action owner. The accountable member must be an active
///     client member and every named evidence request must be an open or fulfilled request in the program. The action id
///     is the logical operation: an exact retry replays the original registration, a different intent under the same id
///     conflicts, and creating an action requires expected revision zero.
/// </summary>
public sealed class AddRiskTreatmentActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AddRiskTreatmentAction, RiskTreatmentActionRegistration>
{
    public async ValueTask<Result<RiskTreatmentActionRegistration>> HandleAsync(
        IRequestContext<AddRiskTreatmentAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.ActionId == Uuid.Empty || request.ExpectedRevision != 0)
            return Fail(new RequestError(RequestErrorKind.Validation,
                "A treatment action requires its action id and is created at expected revision zero."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new RiskTreatmentAction(request.TenantId, request.ProgramId,
            request.RiskId, request.ActionId, request.ActionId);
        var provenance = FindingContent.Provenance(request.ActionId, "AddRiskTreatmentAction", snapshot.Stream,
            actorReference, 0, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? actionContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;

        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAction>(binding,
                () => new RiskTreatmentAction(request.TenantId, request.ProgramId, request.RiskId, request.ActionId,
                    request.ActionId),
                action => action.Acceptance,
                async (action, token) =>
                {
                    try
                    {
                        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId,
                            request.RiskId, token).ConfigureAwait(false);
                        if (!risk.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(risk.Error!);
                        var prepared = action.PrepareAdd();
                        if (!prepared.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                        var invalid = RiskCommands.ValidateAction(request.Title, request.TargetState, request.ExpectedEvidence,
                            request.DueOn, request.AccountableMemberId, now);
                        if (invalid is not null)
                            return Result<FirstResponseDescriptor>.Failure(invalid.ToRequestError());
                        if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.AccountableMemberId, token)
                                .ConfigureAwait(false))
                            return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
                        var evidence = RiskCommands.Distinct(request.EvidenceRequestIds);
                        if (await RiskCommands.RequireCitableEvidenceAsync(reader, request.TenantId, request.ProgramId, evidence, token)
                                .ConfigureAwait(false) is { } unciteable)
                            return Result<FirstResponseDescriptor>.Failure(unciteable);
                        var treatment = (await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId), token)
                            .ConfigureAwait(false)).Header?.TreatmentKind;
                        if (treatment is null or "accept")
                            return Result<FirstResponseDescriptor>.Failure(CommandFailure.StateConflict(
                                "Choose a mitigate, transfer, or avoid treatment before adding treatment actions.").ToRequestError());
                        var view = new RiskTreatmentActionView(request.ActionId, request.RiskId, request.Title.Trim(),
                            request.TargetState.Trim(), request.ExpectedEvidence.Trim(), request.DueOn,
                            request.AccountableMemberId, evidence, RiskStates.ActionOpen, false, actorReference, now, [])
                        {
                            Revision = 1,
                        };
                        actionContent = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.ActionSchema, view, token)
                            .ConfigureAwait(false);
                        (response, result) = await FindingContent.FreezeAsync(content, request.TenantId,
                            RiskContent.ActionRegistrationSchema, new RiskTreatmentActionRegistration(request.ActionId, 1), token)
                            .ConfigureAwait(false);
                        return Result<FirstResponseDescriptor>.Success(response!);
                    }
                    catch (ImmutableContentIntegrityException failure)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact treatment action source content cannot be verified.",
                            failure is ImmutableContentUnavailableException));
                    }
                },
                (RiskTreatmentAction action, ImmutableContentRef frozen) =>
                    action.Add(provenance, actionContent!, frozen, result!)), context, ct).ConfigureAwait(false);
            return await FindingContent.ResponseAsync<RiskTreatmentActionRegistration>(content, request.TenantId, outcome,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<RiskTreatmentActionRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact treatment action content cannot be verified.", failure is ImmutableContentUnavailableException));
        }
    }

    static Result<RiskTreatmentActionRegistration> Fail(RequestError error) =>
        Result<RiskTreatmentActionRegistration>.Failure(error);
}
