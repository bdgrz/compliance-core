using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Rules;

/// <summary>
///     A closure operation as its authoritative owner says it is: the hydrated campaign, the accepted closure fact and
///     the frozen intent with its complete participants. A non-null <see cref="Error" /> is the transient or
///     permanent verification failure; nothing else is set then.
/// </summary>
sealed record PolicyCampaignClosure(PolicyCampaignSnapshot? Campaign, PolicyCampaignClosureAccepted? Accepted,
    ClosureIntent? Intent, IReadOnlyList<ClosureParticipant>? Participants, RequestError? Error)
{
    public static PolicyCampaignClosure Failed(RequestError error) => new(null, null, null, null, error);
}
