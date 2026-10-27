using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>
///     The corrective-action pair of the generic acquire receiver: it holds the exact completed action a closure pinned.
///     An action that is not materialized yet is a transient wait, so an accepted but unmaterialized action blocks the
///     closure without ever failing it. An incomplete action, a closer who owns or completed the work without an exact
///     waiver proof, or a closure that ended aborts the closure and fences every use.
/// </summary>
sealed class AuthorizeCorrectiveActionFindingClosureUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, FindingClosureVerifier verifier, FindingClosureFences fences)
    : AuthorizeSourceUseHandler<CorrectiveAction, AuthorizeCorrectiveActionFindingClosureUse>(reader, executor, content,
        clock)
{
    protected override CorrectiveAction Open(AuthorizeCorrectiveActionFindingClosureUse request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.ProgramId, request.ReceivingOwnerId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeCorrectiveActionFindingClosureUse request, CancellationToken ct)
    {
        var closure = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<SourceUseSource>.Failure(closure.Error);
        var (finding, intent, decision, tenant) = (closure.Finding!, closure.Intent!, closure.Decision!, closure.TenantId);
        IReadOnlyList<AdmittedChild> children;
        try
        {
            children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(Content, tenant.ToString(),
                intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail($"The admitted action manifest cannot be verified: {exception.Message}", true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The admitted action manifest cannot be verified: {exception.Message}", false);
        }

        if (request.ReceivingOwnerId != intent.ParentId || request.ProgramId != decision.ProgramId ||
            request.Generation != (long)intent.ParentGeneration ||
            children.All(child => child.ChildId != request.SourceOwnerId))
            return Fail("The authorization differs from the frozen closure action set.", false);

        var terminal = finding.TerminalClosure(request.OperationId);
        var superseded = terminal is FindingClosedV2 ? "The closure already closed the finding." : null;
        var action = await verifier.HydrateAsync(Open(request, request.CommandId), ct).ConfigureAwait(false);
        var key = FindingClosureEffects.UseKey(intent, action.Stream);
        if (superseded is not null)
            return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key,
                Placeholder(action, intent), nameof(AuthorizeCorrectiveActionFindingClosureUse), null,
                superseded));
        if (!action.IsMaterialized)
            return Fail("The corrective action is not materialized yet.", true);
        if (action.ContentFactById(request.SourceContentEventId) is not { } pinned)
            return Fail("The pinned corrective action fact does not exist.", false);

        var admitted = closure.History!.Admitted.FirstOrDefault(fact => fact.Operation.LogicalId == request.SourceOwnerId);
        if (admitted is null)
            return Fail("The action was not admitted by this finding.", false);
        Uuid actionOwner;
        try
        {
            var admission = await FindingContent.ReadAsync<ChildAdmissionIntent>(Content, tenant, admitted.Intent, ct)
                .ConfigureAwait(false);
            actionOwner = (await FindingContent.ReadAsync<AddCorrectiveAction>(Content, tenant,
                admission.CanonicalAuthoredContent, ct).ConfigureAwait(false)).OwnerMemberId;
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail($"The admitted action cannot be verified: {exception.Message}", true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The admitted action cannot be verified: {exception.Message}", false);
        }

        var completed = action.Completed;
        var waiverProven = intent.WaiverProof is not null;
        var refusal = terminal is FindingClosureCanceled ? "The closure was canceled."
            : completed is null ? "The corrective action is not completed."
            : completed.CompletedByMemberId == decision.CloserMemberId && !waiverProven ||
              actionOwner == decision.CloserMemberId && !waiverProven
                ? "A corrective action owner or completer cannot verify and close their own remediation without a separation-of-duties waiver."
                : null;
        var facts = new ClosureActionFacts(request.SourceOwnerId, actionOwner, completed is not null,
            completed?.CompletedByMemberId, completed is null ? null : NativeFactReference.Of(action.Stream, completed),
            completed?.EvidenceManifest, decision.CloserMemberId, waiverProven, refusal);
        var factsReference = await FindingContent.WriteDeterministicAsync(Content, tenant, request.CommandId,
            FindingContent.ActionFactsSchema, facts, ct).ConfigureAwait(false);
        var requirement = new SourceUseRequirement(action.Stream, NativeFactReference.Of(action.Stream, pinned),
            request.ExpectedContentRevision, SourceUsePurpose.FindingClosure, factsReference);
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key, requirement,
            nameof(AuthorizeCorrectiveActionFindingClosureUse), null, null));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(CorrectiveAction owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (!owner.IsMaterialized)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The corrective action is not materialized yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The corrective action changed after the closure pinned it.", false));
        ClosureActionFacts facts;
        try
        {
            facts = await FindingContent.ReadAsync<ClosureActionFacts>(Content,
                Uuid.Parse(owner.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture), requirement.RequiredFacts, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned action facts cannot be verified: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned action facts cannot be verified: {exception.Message}", false));
        }

        return facts.Refusal is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, facts.Refusal, false));
    }

    protected override bool IsPinned(CorrectiveAction owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<CorrectiveAction, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (CorrectiveAction action, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            action.AuthorizeFindingClosureUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeCorrectiveActionFindingClosureUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var closure = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return closure.Error is not null
            ? Result<ReceiverOutcome>.Failure(closure.Error)
            : await fences.AbortAsync(closure, request.Source, request.Intent, reason, request.SourceOwnerId, context, ct)
                .ConfigureAwait(false);
    }

    /// <summary>A superseded acquisition is a no-op, so its requirement is never pinned or granted.</summary>
    static SourceUseRequirement Placeholder(CorrectiveAction action, ClosureIntent intent) =>
        new(action.Stream, new NativeFactReference(action.Stream, Uuid.Empty, 0), 0, SourceUsePurpose.FindingClosure,
            intent.AuthoredDecision);

    static Uuid Tenant(EventStreamAddress stream) =>
        Uuid.Parse(stream.Realm, System.Globalization.CultureInfo.InvariantCulture);

    static Result<SourceUseSource> Fail(string message, bool transient) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
