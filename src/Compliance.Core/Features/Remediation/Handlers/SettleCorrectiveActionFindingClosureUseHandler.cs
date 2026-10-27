using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

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
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen closure source uses cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
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
