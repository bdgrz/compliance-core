using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Launches completion tracking for the exact latest training requirement version.</summary>
sealed class LaunchTrainingCampaignHandler(CampaignLaunch launch, IAggregateReader reader,
    IImmutableContentStore content) : IRequestHandler<LaunchTrainingCampaign, CampaignRegistration>
{
    public ValueTask<Result<CampaignRegistration>> HandleAsync(IRequestContext<LaunchTrainingCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        return launch.LaunchAsync(context, request.TenantId, request.ProgramId, request.CampaignId,
            nameof(LaunchTrainingCampaign), request, request.DueOn, request.Instructions, ResolveAsync, ct);

        async ValueTask<Result<CampaignLaunchTerms>> ResolveAsync(CancellationToken token)
        {
            var requirement = await reader.HydrateAsync(new TrainingRequirement(request.TenantId,
                request.RequirementId), token).ConfigureAwait(false);
            if (!requirement.IsDefined || requirement.ProgramId != request.ProgramId ||
                requirement.FactOf(request.RequirementVersion) is null)
                return Result<CampaignLaunchTerms>.Failure(new RequestError(
                    RequestErrorKind.NotFound, "The training requirement version was not found."));
            if (request.RequirementVersion != requirement.CurrentVersion)
                return Result<CampaignLaunchTerms>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "Only the latest training requirement version can be launched."));
            var roster = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
                request.RosterSnapshotId, token).ConfigureAwait(false);
            if (!roster.IsSuccess)
                return Result<CampaignLaunchTerms>.Failure(roster.Error);
            TrainingRequirementView view;
            try
            {
                view = (await TrainingRequirementRules.ViewAsync(content, requirement, request.RequirementVersion,
                    token).ConfigureAwait(false))!;
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<CampaignLaunchTerms>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The training requirement content cannot be verified yet: {exception.Message}", true));
            }

            var teams = view.Content.AudienceTeams ?? [];
            var audience = RosterAudience.Evaluate(roster.Value.Roster, view.Content.AudienceKind, teams);
            var subject = new CampaignSubject("training", view.RequirementId, view.Identifier,
                view.Content.CourseName, view.Version, view.ContentSha256);
            return Result<CampaignLaunchTerms>.Success(new CampaignLaunchTerms(subject, view.Content.AudienceKind,
                teams, request.RosterSnapshotId, roster.Value.Roster.ContentSha256, audience));
        }
    }
}
