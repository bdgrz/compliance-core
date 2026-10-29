using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>
///     The frozen launch of a campaign read from its launch fact and verified content: the instructions, deadline,
///     reviewers and every item exactly as launched.
/// </summary>
public sealed record AccessReviewLaunch(Uuid CampaignId, string Name, string Instructions, DateTimeOffset Deadline,
    Uuid SnapshotId, string ContentSha256, IReadOnlyList<AccessReviewerView> Reviewers,
    IReadOnlyList<AccessReviewItemView> Items, ActorReference LaunchedBy, DateTimeOffset LaunchedAt,
    Uuid? ProgramId, Uuid? RemediationOwnerMemberId);
