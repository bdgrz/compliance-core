using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The participant pair of the generic acquire receiver. Result admission is sealed before the closure is
///     accepted, so the participant only has to apply the results the campaign already admitted: until it has, the
///     acquisition waits (transient), never grants early. Once the participant holds exactly the frozen number of
///     applied results, the exact use is granted and no further result can append. A participant that differs from the
///     frozen requirement permanently aborts the closure.
/// </summary>
sealed class AuthorizePolicyCampaignParticipantCampaignClosureUseHandler(IAggregateReader reader,
    IAggregateExecutor executor, IImmutableContentStore content, TimeProvider clock,
    PolicyCampaignClosureVerifier verifier, PolicyCampaignClosureFences fences)
    : AuthorizeSourceUseHandler<PolicyCampaignParticipant, AuthorizePolicyCampaignParticipantCampaignClosureUse>(
        reader, executor, content, clock)
{
    protected override PolicyCampaignParticipant Open(AuthorizePolicyCampaignParticipantCampaignClosureUse request,
        Uuid selectedCommandId) => new(PolicyCampaignContent.ParseTenant(request.Target.Realm), request.CampaignId,
        request.PersonId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizePolicyCampaignParticipantCampaignClosureUse request, CancellationToken ct)
    {
        var closure = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (closure.Error is not null)
            return Result<SourceUseSource>.Failure(closure.Error);
        var (campaign, intent) = (closure.Campaign!, closure.Intent!);
        var participant = closure.Participants!.SingleOrDefault(candidate =>
            candidate.Requirement.SourceOwner == request.Target);
        if (participant is null || participant.PersonId != request.PersonId || request.CampaignId != campaign.Id ||
            Uuid.Parse(participant.Target, System.Globalization.CultureInfo.InvariantCulture) != request.SourceOwnerId ||
            participant.Requirement.ResourceRevision != request.ExpectedContentRevision ||
            participant.Requirement.ContentEvent.EventId != request.SourceContentEventId ||
            request.Generation != (long)intent.ParentGeneration)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The authorization differs from the frozen participant use requirement.", false));
        var superseded = campaign.CanceledFor(request.OperationId) is not null ? "The closure was canceled."
            : campaign.Closing is not null ? "The closure already completed."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            PolicyCampaignClosureEffects.UseKey(request.OperationId, intent.Header.FirstOwner, request.Generation,
                request.Target), participant.Requirement, nameof(AuthorizePolicyCampaignParticipantCampaignClosureUse),
            null, superseded));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(PolicyCampaignParticipant owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (owner.Revision < (long)requirement.ResourceRevision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "An admitted result is not applied to the participant yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The participant holds results the closure did not freeze.", false));
        IReadOnlyList<CampaignAdmittedResult> admitted;
        try
        {
            admitted = await Content.ReadJsonAsync<IReadOnlyList<CampaignAdmittedResult>>(owner.Stream.Realm,
                requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen participant results cannot be verified: {exception.Message}", false));
        }

        return admitted.All(result => owner.ResultFor(result.OperationId) is not null)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The participant applied results other than the ones the closure froze.", false));
    }

    protected override bool IsPinned(PolicyCampaignParticipant owner, SourceUseRequirement requirement) =>
        owner.Revision == (long)requirement.ResourceRevision;

    protected override Func<PolicyCampaignParticipant, OperationProvenance, ImmutableContentRef, long, Result>
        GrantOperation() =>
        (PolicyCampaignParticipant participant, OperationProvenance provenance, ImmutableContentRef intent,
            long generation) => participant.AuthorizeClosureUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizePolicyCampaignParticipantCampaignClosureUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var closure = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return closure.Error is not null
            ? Result<ReceiverOutcome>.Failure(closure.Error)
            : await fences.AbortAndFenceAsync(closure, request.Source, request.Intent, reason, context, ct)
                .ConfigureAwait(false);
    }
}
