using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Readiness.Rules;
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
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
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
