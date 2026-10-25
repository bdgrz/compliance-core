using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Launches acknowledgement of the exact current approved policy version.</summary>
sealed class LaunchPolicyCampaignHandler(CampaignLaunch launch, IAggregateReader reader, PolicyReader policySources)
    : IRequestHandler<LaunchPolicyCampaign, CampaignRegistration>
{
    public ValueTask<Result<CampaignRegistration>> HandleAsync(IRequestContext<LaunchPolicyCampaign> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result<CampaignRegistration>> HandleCoreAsync(IRequestContext<LaunchPolicyCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return launch.LaunchAsync(context, request.TenantId, request.ProgramId, request.CampaignId,
            nameof(LaunchPolicyCampaign), request, request.DueOn, request.Instructions, ResolveAsync, ct);

        async ValueTask<Result<CampaignLaunchTerms>> ResolveAsync(CancellationToken token)
        {
            var policy = await PolicySource.ReadAsync(policySources, request.TenantId, request.ProgramId,
                request.PolicyId, token).ConfigureAwait(false);
            if (!policy.IsSuccess)
                return Result<CampaignLaunchTerms>.Failure(policy.Error);
            if (policy.Value.FindVersion(request.PolicyVersion) is not { } version)
                return Result<CampaignLaunchTerms>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The policy version was not found."));
            if (version.Status != "approved")
                return Result<CampaignLaunchTerms>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "Only the current approved policy version can be distributed."));
            var roster = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
                request.RosterSnapshotId, token).ConfigureAwait(false);
            if (!roster.IsSuccess)
                return Result<CampaignLaunchTerms>.Failure(roster.Error);
            var content = version.Content;
            var teams = content.AudienceTeams ?? [];
            var audience = RosterAudience.Evaluate(roster.Value.Roster, content.AudienceKind, teams);
            var subject = new CampaignSubject("policy", version.PolicyId, version.Identifier,
                content.Title, version.Version, version.ContentSha256);
            return Result<CampaignLaunchTerms>.Success(new CampaignLaunchTerms(subject, content.AudienceKind, teams,
                request.RosterSnapshotId, roster.Value.Roster.ContentSha256, audience));
        }
    }
}
