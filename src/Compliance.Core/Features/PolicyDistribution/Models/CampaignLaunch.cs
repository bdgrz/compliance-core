using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>
///     Launches a campaign as the root native operation of the campaign owner. The campaign id is the logical
///     operation identity: the material, the roster and the complete frozen audience are resolved only for a new
///     operation, so an exact retry replays the original registration even after the policy, the requirement or the
///     roster changed. The launch has no caller revision to compare.
/// </summary>
sealed class CampaignLaunch(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
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
        ImmutableContentRef? result = null;
        ImmutableContentRef? teams = null;
        ImmutableContentRef? entriesContent = null;
        CampaignLaunchTerms? terms = null;
        var launchedOn = DateOnly.FromDateTime(now.UtcDateTime);
        string? trimmed = null;
        var count = 0;
        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(tenantId, campaignId, campaignId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var prepared = owner.PrepareLaunch();
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var resolved = await resolve(token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(resolved.Error!);
                terms = resolved.Value!;
                if (CampaignRules.CheckLaunch(dueOn, launchedOn, instructions) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                trimmed = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
                var entries = CampaignRules.Entries(terms.Audience, dueOn);
                count = entries.Count;
                teams = await PolicyCampaignContent.WriteAsync(content, tenantId, PolicyCampaignContent.TeamsSchema,
                    terms.Teams, token).ConfigureAwait(false);
                entriesContent = await PolicyCampaignContent.WriteAsync(content, tenantId,
                    PolicyCampaignContent.EntriesSchema, entries, token).ConfigureAwait(false);
                var body = await PolicyCampaignContent.WriteAsync(content, tenantId,
                    PolicyCampaignContent.RegistrationSchema, new CampaignRegistration(campaignId, count, 1), token)
                    .ConfigureAwait(false);
                result = await PolicyCampaignContent.WriteResultAsync(content, tenantId,
                    PolicyCampaignContent.RegistrationSchema, body, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(PolicyCampaignContent.OkResponse(
                    PolicyCampaignContent.RegistrationSchema, body));
            },
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) => owner.LaunchCampaign(provenance,
                programId, terms!.Subject, terms.AudienceKind, teams!, terms.RosterSnapshotId,
                terms.RosterContentSha256, launchedOn, dueOn, trimmed, count, actor.Reference, actor.MemberId, now,
                entriesContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<CampaignRegistration>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<CampaignRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CampaignRegistration>.Failure(new RequestError(kind, message));
}

/// <summary>The exact material, audience rule and roster a new launch freezes.</summary>
sealed record CampaignLaunchTerms(CampaignSubject Subject, string AudienceKind, IReadOnlyList<string> Teams,
    Uuid RosterSnapshotId, string RosterContentSha256, RosterAudience Audience);
