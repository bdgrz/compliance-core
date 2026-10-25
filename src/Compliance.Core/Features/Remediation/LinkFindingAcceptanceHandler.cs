using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Links an existing approved acceptance owned by another workflow: an R1-07 risk acceptance in this program or an
///     approved separation-of-duties waiver. Its expiry is copied so the finding returns to an actionable state when the
///     acceptance lapses. The complete acceptance is verified content and the frozen first response is the finding view
///     after the fact.
/// </summary>
public sealed class LinkFindingAcceptanceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    FindingReader findings, IImmutableContentStore content, TimeProvider clock, RiskEvaluationReader evaluations)
    : IRequestHandler<LinkFindingAcceptance, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<LinkFindingAcceptance> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An acceptance link requires a command id and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "LinkFindingAcceptance", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? acceptanceContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var prepared = owner.PrepareWrite(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var expiry = await ExpiryAsync(request, token).ConfigureAwait(false);
            if (!expiry.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(expiry.Error!);
            var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
            if (current!.Acceptances.Any(item => item.Kind == request.Kind && item.RecordId == request.RecordId &&
                                                 item.DecisionId == request.DecisionId))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The acceptance is already linked to this finding."));
            if (expiry.Value <= now)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "An expired acceptance cannot be linked."));
            var acceptance = new FindingAcceptanceView(request.Kind, request.RecordId, request.DecisionId, expiry.Value,
                actorReference, now);
            acceptanceContent = await FindingContent.WriteAsync(content, request.TenantId,
                FindingContent.AcceptanceSchema, acceptance, token).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithAcceptance(current, acceptance, owner.Revision + 1, now), token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef frozen) =>
                finding.LinkAcceptance(provenance, acceptanceContent!, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<DateTimeOffset>> ExpiryAsync(LinkFindingAcceptance request, CancellationToken ct)
    {
        try
        {
            DateTimeOffset? expiresAt = null;
            if (request.Kind == FindingStates.RiskAcceptance && request.DecisionId is { } decision)
            {
                var risk = await evaluations.ReadAsync(request.TenantId, request.ProgramId, request.RecordId, ct).ConfigureAwait(false);
                if (risk.ProgramId == request.ProgramId && risk.FindAcceptance(decision) is { } accepted)
                    expiresAt = accepted.ExpiresAt;
            }
            else if (request.Kind == FindingStates.Waiver && request.DecisionId is null)
            {
                var waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, request.RecordId), ct)
                    .ConfigureAwait(false);
                if (waiver.IsRecorded && waiver.ApprovedAt is not null)
                    expiresAt = waiver.ExpiresAt;
            }
            else
                return Result<DateTimeOffset>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Link a risk_acceptance with its risk and acceptance IDs, or an approved waiver by its ID."));
            return expiresAt is { } resolved
                ? Result<DateTimeOffset>.Success(resolved)
                : Result<DateTimeOffset>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The acceptance was not found or is not approved in this program."));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<DateTimeOffset>.Failure(RiskEvaluationReadFailure.Of(failure));
        }
    }
}
