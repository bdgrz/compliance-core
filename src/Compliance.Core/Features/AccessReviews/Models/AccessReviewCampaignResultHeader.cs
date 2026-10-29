using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>Links a final campaign snapshot to the launch snapshot it completes.</summary>
public sealed record AccessReviewCampaignResultHeader(Uuid CampaignId, string Name,
    DateTimeOffset Deadline, Uuid LaunchSnapshotId, string LaunchContentSha256,
    string Attestation);
