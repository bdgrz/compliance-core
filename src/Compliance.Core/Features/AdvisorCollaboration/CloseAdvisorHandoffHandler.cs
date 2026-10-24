using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Closes an open handoff at the expected handoff revision. Closure and feedback admission share the handoff
///     stream: feedback admitted before the closure stays admitted and is still materialized, and feedback racing the
///     closure is refused by the stream's revision.
/// </summary>
public sealed class CloseAdvisorHandoffHandler(AdvisorUnitOperation operation, TimeProvider clock)
    : IRequestHandler<CloseAdvisorHandoff>
{
    const string CommandKind = "CloseAdvisorHandoff";

    public ValueTask<Result> HandleAsync(IRequestContext<CloseAdvisorHandoff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Closing an advisor handoff requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "Closing an advisor handoff requires command, program and handoff identities and a non-negative expected revision."));
        if (string.IsNullOrWhiteSpace(request.Rationale))
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid("Ending a handoff requires a rationale and actor."));

        var now = clock.GetUtcNow();
        var canonical = request with { Rationale = request.Rationale.Trim() };
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        return operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.CommandId),
            handoff => handoff.Acceptance, null,
            handoff => AdvisorHandoffRules.InProgram(handoff, request.ProgramId)
                ? handoff.PrepareEnd(request.ExpectedRevision)
                : AdvisorHandoffRules.NotFound(),
            (handoff, frozen, result) => handoff.Close(provenance, canonical.Rationale, actor, now, frozen, result), ct);
    }
}
