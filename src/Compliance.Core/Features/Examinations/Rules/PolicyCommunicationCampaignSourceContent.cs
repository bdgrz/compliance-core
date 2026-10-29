using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Rules;

/// <summary>
///     The complete point-in-time content of a campaign for a Type I baseline. The teams, the frozen audience, every
///     amendment's items and the closing totals are the expanded originals of the manifests the campaign facts
///     reference, so the digest commits to all of them.
/// </summary>
sealed record PolicyCommunicationCampaignSourceContent(DateOnly AsOf,
    PolicyCampaignLaunched Launch, IReadOnlyList<string> AudienceTeams, int FrozenAudienceBatchCount,
    IReadOnlyList<CampaignAudienceEntry> FrozenAudience,
    IReadOnlyList<PolicyCommunicationAudienceAmendment> AudienceAmendments,
    IReadOnlyList<PolicyAcknowledgementRecorded> Acknowledgements,
    IReadOnlyList<TrainingCompletionRecorded> Completions,
    IReadOnlyList<CampaignWaiverApproved> Waivers, PolicyCampaignClosed? Closure,
    CampaignTotalsView? ClosingTotals);

/// <summary>One reconciliation fact with the complete list of amendments its manifest holds.</summary>
sealed record PolicyCommunicationAudienceAmendment(PolicyCampaignAudienceAmended Fact,
    IReadOnlyList<CampaignAudienceAmendment> Items);
