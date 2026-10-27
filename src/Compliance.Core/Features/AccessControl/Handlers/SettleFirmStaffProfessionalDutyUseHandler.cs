using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The staff-record pair of the generic settle receiver. The terminal fact is resolved from the designation owner:
///     a recorded designation requires <c>irreversible_decision</c>, a rejected change requires <c>canceled</c>. A
///     missing terminal fact is a transient wait, never an inferred rejection.
/// </summary>
sealed class SettleFirmStaffProfessionalDutyUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ProfessionalDutyChangeVerifier verifier)
    : SettleSourceUseHandler<FirmStaffRecord, SettleFirmStaffProfessionalDutyUse>(reader, executor, content, clock)
{
    protected override FirmStaffRecord Open(SettleFirmStaffProfessionalDutyUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(SettleFirmStaffProfessionalDutyUse request,
        CancellationToken ct)
    {
        var change = await verifier.VerifyAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<SourceUseSource>.Failure(change.Error);
        var (intent, owner) = (change.Intent!, change.Owner!);
        if (intent.StaffEligibility is not { } requirement ||
            Uuid.Parse(requirement.SourceOwner.Resource, CultureInfo.InvariantCulture) != request.SourceOwnerId ||
            intent.DesignationId != request.DesignationId || request.Generation != ProfessionalDutyEffects.Generation)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The settlement differs from the frozen staff use requirement.", false));
        var disposition = owner.TerminalFor(request.OperationId) switch
        {
            FirmProfessionalDutyDesignationRecorded => SourceUseSettlementDisposition.IrreversibleDecision,
            ProfessionalDutyChangeRejected => SourceUseSettlementDisposition.Canceled,
            _ => (SourceUseSettlementDisposition?)null,
        };
        return disposition is null
            ? Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Neither the recorded designation nor the rejection is visible yet.", true))
            : Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
                ProfessionalDutyRules.StaffUseKey(request.OperationId, owner.Stream, requirement), requirement,
                nameof(SettleFirmStaffProfessionalDutyUse), disposition, null));
    }

    protected override Func<FirmStaffRecord, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (FirmStaffRecord record, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            record.SettleProfessionalDutyUse(provenance, intent, generation);
}
