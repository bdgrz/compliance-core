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
///     Replaces a handoff's selected versions at the expected handoff revision. The revision is a fact of the handoff
///     stream and advances the selection generation, so feedback admitted against the previous selection is
///     historical and a revision racing a feedback admission is refused by the stream's revision.
/// </summary>
public sealed class ReviseAdvisorHandoffSelectionHandler(AdvisorUnitOperation operation, AdvisorHandoffReader handoffs,
    IAdvisorRecordVersionResolver recordVersions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseAdvisorHandoffSelection>
{
    const string CommandKind = "ReviseAdvisorHandoffSelection";

    public ValueTask<Result> HandleAsync(IRequestContext<ReviseAdvisorHandoffSelection> context,
        CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<ReviseAdvisorHandoffSelection> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return AdvisorHandoffRules.Forbidden("Revising an advisor handoff requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AdvisorHandoffRules.Invalid(
                "A handoff selection revision requires command, program and handoff identities and a non-negative expected revision.");
        if (!AdvisorHandoffRules.ValidSelection(request.RecordVersions) || string.IsNullOrWhiteSpace(request.ChangeReason))
            return AdvisorHandoffRules.Invalid("A selection revision requires exact versions, a reason, and an actor.");

        var now = clock.GetUtcNow();
        var canonical = request with { ChangeReason = request.ChangeReason.Trim() };
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? manifest = null;
        ImmutableContentRef? authored = null;

        return await operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.CommandId),
            handoff => handoff.Acceptance,
            async (hydrated, token) =>
            {
                var exactSources = await recordVersions.RequireCurrentAsync(request.TenantId, request.ProgramId,
                    request.RecordVersions, token).ConfigureAwait(false);
                if (!exactSources.IsSuccess)
                    return exactSources;
                if (!AdvisorHandoffRules.InProgram(hydrated, request.ProgramId))
                    return AdvisorHandoffRules.NotFound();
                // A stale revision is reported by the owner guard; the checks below describe the selection at the
                // expected revision only.
                if (hydrated.Revision == request.ExpectedRevision && hydrated.IsOpen)
                {
                    var current = await handoffs.ReadAsync(hydrated, request.TenantId, token).ConfigureAwait(false);
                    if (current.Selection.SequenceEqual(request.RecordVersions))
                        return AdvisorHandoffRules.Invalid("The revised selection must change the handoff selection.");
                    if (hydrated.ComparedToHandoffId is { } comparedToId)
                    {
                        var previous = await handoffs.ReadAsync(request.TenantId, request.ProgramId, comparedToId,
                            token).ConfigureAwait(false);
                        if (!previous.IsCreated || previous.ProgramId != request.ProgramId ||
                            !previous.WasReadyForReview ||
                            previous.Status is not (AdvisorHandoff.Closed or AdvisorHandoff.Revoked))
                            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The handoff comparison baseline is no longer available."));
                        if (request.RecordVersions.Any(selected =>
                                AdvisorHandoffSelectionComparison.Classify(selected, previous.Selection) ==
                                AdvisorHandoffSourceChange.Unchanged))
                            return AdvisorHandoffRules.Invalid(
                                "A repeated handoff may include only new or changed source versions.");
                    }
                }

                manifest = await AdvisorContent.WriteSelectionAsync(content, request.TenantId,
                    AdvisorContent.RevisedSelectionSchema, request.RecordVersions, token).ConfigureAwait(false);
                authored = await AdvisorAuthored.WriteAsync(content, request.TenantId, canonical, token).ConfigureAwait(false);
                return Result.Success;
            },
            handoff => handoff.PrepareRevise(request.ExpectedRevision),
            (handoff, frozen, result) => handoff.ReviseSelection(provenance, manifest!, authored!, actor,
                now, frozen, result), ct).ConfigureAwait(false);
    }
}
