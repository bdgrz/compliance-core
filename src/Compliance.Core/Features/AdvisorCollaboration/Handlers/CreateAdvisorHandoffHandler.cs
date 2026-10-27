using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Creates one attributed draft handoff for a program's compliance lead. The handoff identity is the logical
///     operation, so an exact retry replays the original result even after the handoff changed, and a different
///     creation under the same identity conflicts. The complete exact selection is verified content.
/// </summary>
public sealed class CreateAdvisorHandoffHandler(AdvisorUnitOperation operation, AdvisorHandoffReader handoffs,
    IAdvisorRecordVersionResolver recordVersions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CreateAdvisorHandoff>
{
    const string CommandKind = "CreateAdvisorHandoff";

    public ValueTask<Result> HandleAsync(IRequestContext<CreateAdvisorHandoff> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<CreateAdvisorHandoff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return AdvisorHandoffRules.Forbidden("Creating an advisor handoff requires a personal HTTP actor.");
        if (request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty)
            return AdvisorHandoffRules.Invalid("An advisor handoff requires program and handoff identities.");
        if (string.IsNullOrWhiteSpace(request.Purpose) || !AdvisorHandoffRules.ValidSelection(request.RecordVersions) ||
            !AdvisorHandoffRules.ValidActor(actor) || request.ComparedToHandoffId is { } compareId &&
            (compareId == Uuid.Empty || compareId == request.HandoffId))
            return AdvisorHandoffRules.Invalid(
                "A handoff requires purpose, exact selected versions, an actor, and a valid expiry.");

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var canonical = request with { Purpose = request.Purpose.Trim() };
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.HandoffId, CommandKind, owner, actor, null,
            AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? manifest = null;
        ImmutableContentRef? authored = null;

        return await operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.HandoffId),
            handoff => handoff.Acceptance,
            async (_, token) =>
            {
                if (request.ExpiresOn < today)
                    return AdvisorHandoffRules.Invalid(
                        "A handoff requires purpose, exact selected versions, an actor, and a valid expiry.");
                var exactSources = await recordVersions.RequireCurrentAsync(request.TenantId, request.ProgramId,
                    request.RecordVersions, token).ConfigureAwait(false);
                if (!exactSources.IsSuccess)
                    return exactSources;
                if (request.ComparedToHandoffId is { } comparedToId)
                {
                    var previous = await handoffs.ReadAsync(request.TenantId, request.ProgramId, comparedToId, token)
                        .ConfigureAwait(false);
                    if (!previous.IsCreated || previous.ProgramId != request.ProgramId)
                        return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                            "The comparison handoff was not found in the requested program."));
                    if (!previous.WasReadyForReview || previous.Status is not (AdvisorHandoff.Closed or AdvisorHandoff.Revoked))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "A repeated handoff can compare only with a shared handoff that has ended."));
                    if (request.RecordVersions.Any(selected =>
                            AdvisorHandoffSelectionComparison.Classify(selected, previous.Selection) ==
                            AdvisorHandoffSourceChange.Unchanged))
                        return AdvisorHandoffRules.Invalid(
                            "A repeated handoff may include only new or changed source versions.");
                }

                manifest = await AdvisorContent.WriteSelectionAsync(content, request.TenantId,
                    AdvisorContent.CreatedSelectionSchema, request.RecordVersions, token).ConfigureAwait(false);
                authored = await AdvisorAuthored.WriteAsync(content, request.TenantId, canonical, token).ConfigureAwait(false);
                return Result.Success;
            },
            handoff => handoff.PrepareCreate(),
            (handoff, frozen, result) => handoff.Create(provenance, manifest!, authored!, request.ExpiresOn,
                actor, now, request.ComparedToHandoffId, frozen, result), ct).ConfigureAwait(false);
    }
}
