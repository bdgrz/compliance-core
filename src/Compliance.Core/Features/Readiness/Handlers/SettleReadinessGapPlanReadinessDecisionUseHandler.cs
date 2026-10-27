using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>
///     The gap plan pair of the generic settle receiver. The terminal fact is resolved from the program readiness: a
///     recorded decision requires <c>irreversible_decision</c>, which releases the pinned plan after the historical
///     decision, and a canceled decision requires <c>canceled</c>. A missing terminal fact is a transient wait, never an
///     inferred cancellation. The settled requirement is the one the decision froze for this plan at admission.
/// </summary>
sealed class SettleReadinessGapPlanReadinessDecisionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ReadinessVerifier verifier)
    : SettleSourceUseHandler<ReadinessGapPlan, SettleReadinessGapPlanReadinessDecisionUse>(reader, executor, content,
        clock)
{
    protected override ReadinessGapPlan Open(SettleReadinessGapPlanReadinessDecisionUse request, Uuid selectedCommandId) =>
        new(Uuid.Parse(request.Target.Realm, CultureInfo.InvariantCulture), request.ReceivingOwnerId,
            request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleReadinessGapPlanReadinessDecisionUse request, CancellationToken ct)
    {
        var decision = await verifier.VerifyTerminalAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (decision.Error is not null)
            return Result<SourceUseSource>.Failure(decision.Error);
        var (program, intent, tenant) = (decision.Program!, decision.Intent!, decision.TenantId);
        if (request.ReceivingOwnerId != intent.ProgramId || request.Generation != decision.Generation)
            return Fail("The settlement differs from the frozen decision intent.");
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(Content, tenant.ToString(),
                intent.RequiredPlanUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen decision plan uses cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var plan = Open(request, request.CommandId);
        if (uses.FirstOrDefault(use => use.SourceOwner == plan.Stream) is not { } requirement)
            return Fail("The gap is not a frozen plan use of this decision.");
        var disposition = program.TerminalDecision(request.OperationId) switch
        {
            ReadinessDecisionRecorded or TypeIEntryDecisionRecorded => SourceUseSettlementDisposition.IrreversibleDecision,
            ReadinessDecisionCanceled => SourceUseSettlementDisposition.Canceled,
            _ => (SourceUseSettlementDisposition?)null,
        };
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            ReadinessEffects.UseKey(intent, decision.Generation, plan.Stream), requirement,
            nameof(SettleReadinessGapPlanReadinessDecisionUse), disposition, null));
    }

    protected override Func<ReadinessGapPlan, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (ReadinessGapPlan plan, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            plan.SettleReadinessDecisionUse(provenance, intent, generation);

    static Result<SourceUseSource> Fail(string message, bool transient = false) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
