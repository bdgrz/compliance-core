using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     The corrective-action pair of the generic settle receiver. The terminal fact is resolved from the finding: a
///     closed finding requires <c>parent_closed</c>, which installs the closed-generation fence, and a canceled closure
///     requires <c>canceled</c>. A missing terminal fact is a transient wait, never an inferred cancellation. The
///     settled requirement is the one the closure froze for this action at admission.
/// </summary>
sealed class SettleCorrectiveActionFindingClosureUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, FindingClosureVerifier verifier)
    : SettleSourceUseHandler<CorrectiveAction, SettleCorrectiveActionFindingClosureUse>(reader, executor, content, clock)
{
    protected override CorrectiveAction Open(SettleCorrectiveActionFindingClosureUse request, Uuid selectedCommandId) =>
        new(Uuid.Parse(request.Target.Realm, System.Globalization.CultureInfo.InvariantCulture), request.ProgramId,
            request.ReceivingOwnerId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleCorrectiveActionFindingClosureUse request, CancellationToken ct)
    {
        var closure = await verifier.VerifyTerminalAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<SourceUseSource>.Failure(closure.Error);
        var (finding, intent, decision, tenant) = (closure.Finding!, closure.Intent!, closure.Decision!, closure.TenantId);
        if (request.ReceivingOwnerId != intent.ParentId || request.ProgramId != decision.ProgramId ||
            request.Generation != (long)intent.ParentGeneration)
            return Fail("The settlement differs from the frozen closure intent.");
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(Content, tenant.ToString(),
                intent.SourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen closure source uses cannot be verified: {exception.Message}");
        }

        var action = Open(request, request.CommandId);
        if (uses.FirstOrDefault(use => use.SourceOwner == action.Stream) is not { } requirement)
            return Fail("The action is not a frozen source use of this closure.");
        var disposition = finding.TerminalClosure(request.OperationId) switch
        {
            FindingClosedV2 => SourceUseSettlementDisposition.ParentClosed,
            FindingClosureCanceled => SourceUseSettlementDisposition.Canceled,
            _ => (SourceUseSettlementDisposition?)null,
        };
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            FindingClosureEffects.UseKey(intent, action.Stream), requirement,
            nameof(SettleCorrectiveActionFindingClosureUse), disposition, null));
    }

    protected override Func<CorrectiveAction, OperationProvenance, ImmutableContentRef, long,
        SourceUseSettlementDisposition, Result> SettleOperationWithDisposition() =>
        (CorrectiveAction action, OperationProvenance provenance, ImmutableContentRef intent, long generation,
            SourceUseSettlementDisposition disposition) =>
            action.SettleFindingClosureUse(provenance, intent, generation, disposition);

    static Result<SourceUseSource> Fail(string message) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));
}
