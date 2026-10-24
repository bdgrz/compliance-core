using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Launches a campaign as the root native operation of the campaign owner. The campaign id is the logical
///     operation identity: the material, the roster and the complete frozen audience are resolved only for a new
///     operation, so an exact retry replays the original registration even after the policy, the requirement or the
///     roster changed. The launch has no caller revision to compare.
/// </summary>
sealed class CampaignLaunch(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<CampaignRegistration>> LaunchAsync<TRequest>(IRequestContext<TRequest> context,
        Uuid tenantId, Uuid programId, Uuid campaignId, string commandKind, TRequest request, DateOnly dueOn,
        string? instructions, Func<CancellationToken, ValueTask<Result<CampaignLaunchTerms>>> resolve,
        CancellationToken ct) where TRequest : IRequest<CampaignRegistration>
    {
        if (campaignId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A campaign launch requires a campaign id.");
        var actor = PolicyActor.From(context, tenantId);
        var now = clock.GetUtcNow();
        var stream = new PolicyDistributionCampaign(tenantId, campaignId).Stream;
        var provenance = PolicyCampaignContent.Provenance(campaignId, commandKind, stream, stream, actor.Reference,
            null, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId, campaignId, campaignId), ct)
            .ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? teams = null;
        ImmutableContentRef? entriesContent = null;
        CampaignLaunchTerms? terms = null;
        var launchedOn = DateOnly.FromDateTime(now.UtcDateTime);
        string? trimmed = null;
        var count = 0;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = current.PrepareLaunch();
            if (!prepared.IsSuccess)
                return Result<CampaignRegistration>.Failure(prepared.Error!);
            var resolved = await resolve(ct).ConfigureAwait(false);
            if (!resolved.IsSuccess)
                return Result<CampaignRegistration>.Failure(resolved.Error!);
            terms = resolved.Value!;
            if (CampaignRules.CheckLaunch(dueOn, launchedOn, instructions) is { } failure)
                return Result<CampaignRegistration>.Failure(failure.ToRequestError());
            trimmed = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
            var entries = CampaignRules.Entries(terms.Audience, dueOn);
            count = entries.Count;
            teams = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.TeamsSchema,
                terms.Teams, ct).ConfigureAwait(false);
            entriesContent = await PolicyCampaignContent.WriteAsync(content, tenantId,
                PolicyCampaignContent.EntriesSchema, entries, ct).ConfigureAwait(false);
            var body = await PolicyCampaignContent.WriteAsync(content, tenantId,
                PolicyCampaignContent.RegistrationSchema, new CampaignRegistration(campaignId, count, 1), ct)
                .ConfigureAwait(false);
            result = await PolicyCampaignContent.WriteResultAsync(content, tenantId,
                PolicyCampaignContent.RegistrationSchema, body, ct).ConfigureAwait(false);
            response = PolicyCampaignContent.OkResponse(PolicyCampaignContent.RegistrationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(tenantId, campaignId, campaignId), owner => owner.Acceptance,
            owner => Prepare(owner, response),
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) => owner.LaunchCampaign(provenance,
                programId, terms!.Subject, terms.AudienceKind, teams!, terms.RosterSnapshotId,
                terms.RosterContentSha256, launchedOn, dueOn, trimmed, count, actor.Reference, actor.MemberId, now,
                entriesContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<CampaignRegistration>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(PolicyDistributionCampaign owner, FirstResponseDescriptor? response)
    {
        var prepared = owner.PrepareLaunch();
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The campaign changed while the launch was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<CampaignRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CampaignRegistration>.Failure(new RequestError(kind, message));
}

/// <summary>The exact material, audience rule and roster a new launch freezes.</summary>
sealed record CampaignLaunchTerms(CampaignSubject Subject, string AudienceKind, IReadOnlyList<string> Teams,
    Uuid RosterSnapshotId, string RosterContentSha256, RosterAudience Audience);
