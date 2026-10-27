using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

sealed class ListCampaignParticipantsHandler(CampaignSource campaigns, TimeProvider clock)
    : IRequestHandler<ListCampaignParticipants, Page<CampaignParticipantView>>
{
    static readonly string[] States =
        ["pending", "overdue", "acknowledged", "completed", "excepted", "removed"];

    public ValueTask<Result<Page<CampaignParticipantView>>> HandleAsync(
        IRequestContext<ListCampaignParticipants> context, CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<Page<CampaignParticipantView>>> HandleCoreAsync(
        IRequestContext<ListCampaignParticipants> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.State is { } state && !States.Contains(state))
            return Result<Page<CampaignParticipantView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The state must be pending, overdue, acknowledged, completed, excepted, or removed."));
        var campaign = await campaigns.ReadAsync(request.TenantId, request.ProgramId, request.CampaignId, ct)
            .ConfigureAwait(false);
        return campaign.IsSuccess
            ? ControlActivationSource.Paginate(campaign.Value.ParticipantViews(request.AsOf ??
                PolicySource.Today(clock), request.State), request.Limit, request.Cursor, "campaign participants")
            : Result<Page<CampaignParticipantView>>.Failure(campaign.Error);
    }
}
