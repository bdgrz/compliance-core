using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>Launches completion tracking for the exact latest training requirement version.</summary>
sealed class LaunchTrainingCampaignHandler(CampaignLaunch launch, IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content) : IRequestHandler<LaunchTrainingCampaign, CampaignRegistration>
{
    public ValueTask<Result<CampaignRegistration>> HandleAsync(IRequestContext<LaunchTrainingCampaign> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result<CampaignRegistration>> HandleCoreAsync(IRequestContext<LaunchTrainingCampaign> context,
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
                request.RequirementVersion < 1 || request.RequirementVersion > requirement.CurrentVersion)
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
                view = (await TrainingRequirementRules.ViewAsync(content, events, requirement, request.RequirementVersion,
                    token).ConfigureAwait(false))!;
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<CampaignLaunchTerms>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The training requirement content cannot be verified: {exception.Message}",
                    exception is ImmutableContentUnavailableException));
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
