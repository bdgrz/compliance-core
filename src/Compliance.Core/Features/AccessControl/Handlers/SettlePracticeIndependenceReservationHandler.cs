using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The practice subject pair of the generic settle receiver. The terminal fact is resolved from the engagement
///     owner; a completed operation requires its exact permanent commitment, a rejected one fences the key so a
///     delayed reservation is refused. A settlement never removes a permanent commitment.
/// </summary>
sealed class SettlePracticeIndependenceReservationHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, EngagementAcceptanceVerifier verifier)
    : SettleSourceUseHandler<PracticeIndependenceCommitment, SettlePracticeIndependenceReservation>(reader, executor,
        content, clock)
{
    protected override PracticeIndependenceCommitment Open(SettlePracticeIndependenceReservation request,
        Uuid selectedCommandId) =>
        new(EngagementPersonalDecisionVerifier.TenantOf(request.Target), request.SubjectKind, request.SubjectId,
            selectedCommandId);

    protected override ValueTask<Result<SourceUseSource>> VerifyAsync(SettlePracticeIndependenceReservation request,
        CancellationToken ct) => verifier.SettleAsync(request.Source, request.Intent, request.OperationId, Uuid.Empty,
        request.CommandId, request.Target, request.Generation, nameof(SettlePracticeIndependenceReservation), ct);

    protected override Func<PracticeIndependenceCommitment, OperationProvenance, ImmutableContentRef, long, Result> SettleOperation() =>
        (PracticeIndependenceCommitment owner, OperationProvenance provenance, ImmutableContentRef intent,
            long generation) => owner.SettleReservation(provenance, intent, generation);
}
