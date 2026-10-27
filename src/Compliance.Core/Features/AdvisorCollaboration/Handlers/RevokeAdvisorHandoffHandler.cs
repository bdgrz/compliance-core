using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Revokes an open handoff at the expected handoff revision. Revocation and feedback admission share the handoff
///     stream: feedback admitted before the revocation stays admitted and is still materialized, and feedback racing
///     the revocation is refused by the stream's revision.
/// </summary>
public sealed class RevokeAdvisorHandoffHandler(AdvisorUnitOperation operation, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RevokeAdvisorHandoff>
{
    const string CommandKind = "RevokeAdvisorHandoff";

    public ValueTask<Result> HandleAsync(IRequestContext<RevokeAdvisorHandoff> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result> HandleCoreAsync(IRequestContext<RevokeAdvisorHandoff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Revoking an advisor handoff requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "Revoking an advisor handoff requires command, program and handoff identities and a non-negative expected revision."));
        if (string.IsNullOrWhiteSpace(request.Rationale))
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid("Ending a handoff requires a rationale and actor."));

        var now = clock.GetUtcNow();
        var canonical = request with { Rationale = request.Rationale.Trim() };
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? authored = null;
        return operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.CommandId),
            handoff => handoff.Acceptance,
            async (_, token) =>
            {
                authored = await AdvisorAuthored.WriteAsync(content, request.TenantId, canonical, token).ConfigureAwait(false);
                return Result.Success;
            },
            handoff => AdvisorHandoffRules.InProgram(handoff, request.ProgramId)
                ? handoff.PrepareEnd(request.ExpectedRevision)
                : AdvisorHandoffRules.NotFound(),
            (handoff, frozen, result) => handoff.Revoke(provenance, authored!, actor, now, frozen, result), ct);
    }
}
