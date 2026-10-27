using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
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
///     The gap plan pair of the generic acquire receiver: it holds the exact plan, or the exact absence of one, that a
///     decision froze at admission. A plan that changed before it was held, a decision that was canceled, or frozen
///     content that cannot be proven refuses the use and fences the decision instead of silently reauthoring it. A
///     decision that is already recorded makes the acquisition a terminal no-op.
/// </summary>
sealed class AuthorizeReadinessGapPlanReadinessDecisionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ReadinessVerifier verifier, ReadinessDecisionFences fences)
    : AuthorizeSourceUseHandler<ReadinessGapPlan, AuthorizeReadinessGapPlanReadinessDecisionUse>(reader, executor,
        content, clock)
{
    protected override ReadinessGapPlan Open(AuthorizeReadinessGapPlanReadinessDecisionUse request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.ReceivingOwnerId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeReadinessGapPlanReadinessDecisionUse request, CancellationToken ct)
    {
        var decision = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (decision.Error is not null)
            return Result<SourceUseSource>.Failure(decision.Error);
        var (program, intent, tenant) = (decision.Program!, decision.Intent!, decision.TenantId);
        if (request.ReceivingOwnerId != intent.ProgramId || request.Generation != decision.Generation)
            return Fail("The authorization differs from the frozen decision intent.", false);

        IReadOnlyList<SourceUseRequirement> uses;
        IReadOnlyList<ReadinessGapView> gaps;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(Content, tenant.ToString(),
                intent.RequiredPlanUses, ct).ConfigureAwait(false);
            gaps = await ImmutableSelectionManifests.ReadAsync<ReadinessGapView>(Content, tenant.ToString(),
                intent.GapSelection, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen decision manifests cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
        }

        var plan = Open(request, request.CommandId);
        if (uses.FirstOrDefault(use => use.SourceOwner == plan.Stream) is not { } frozen ||
            gaps.All(gap => gap.GapId != request.SourceOwnerId))
            return Fail("The gap is not a frozen plan use of this decision.", false);
        var key = ReadinessEffects.UseKey(intent, decision.Generation, plan.Stream);
        var terminal = program.TerminalDecision(request.OperationId);
        if (terminal is ReadinessDecisionRecorded or TypeIEntryDecisionRecorded or ReadinessDecisionCanceled)
            return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key, frozen,
                nameof(AuthorizeReadinessGapPlanReadinessDecisionUse), null,
                terminal is ReadinessDecisionCanceled ? "The decision was canceled." : "The decision is already recorded.")
            {
                RequiresAbort = terminal is ReadinessDecisionCanceled,
            });
        if (request.SourceContentEventId != frozen.ContentEvent.EventId ||
            request.ExpectedContentRevision != frozen.ResourceRevision)
            return Fail("The authorization differs from the frozen gap plan use.", false);

        var planned = gaps.First(gap => gap.GapId == request.SourceOwnerId).Plan;
        var facts = new ReadinessPlanFacts(request.SourceOwnerId, planned,
            terminal is ReadinessDecisionCanceled ? "The decision was canceled." : null);
        var factsReference = await ReadinessContent.WriteDeterministicAsync(Content, tenant, request.CommandId,
            ReadinessContent.PlanFactsSchema, facts, ct).ConfigureAwait(false);
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key,
            frozen with { RequiredFacts = factsReference },
            nameof(AuthorizeReadinessGapPlanReadinessDecisionUse), null, null));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(ReadinessGapPlan owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The gap plan changed after the decision froze it.", false));
        ReadinessPlanFacts facts;
        try
        {
            facts = await ReadinessContent.ReadAsync<ReadinessPlanFacts>(Content,
                Uuid.Parse(owner.Stream.Realm, CultureInfo.InvariantCulture), requirement.RequiredFacts, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned plan facts cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        return facts.Refusal is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, facts.Refusal, false));
    }

    protected override bool IsPinned(ReadinessGapPlan owner, SourceUseRequirement requirement) =>
        (owner.Latest?.Metadata!.EventId ?? Uuid.Empty) == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<ReadinessGapPlan, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (ReadinessGapPlan plan, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            plan.AuthorizeReadinessDecisionUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeReadinessGapPlanReadinessDecisionUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var decision = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return decision.Error is not null
            ? Result<ReceiverOutcome>.Failure(decision.Error)
            : await fences.AbortAsync(decision, request.Source, request.Intent, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);

    static Result<SourceUseSource> Fail(string message, bool transient) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
