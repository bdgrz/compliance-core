using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

/// <summary>The receiver names of the email challenge delivery process, as the process catalog spells them.</summary>
public static class EmailChallengeDeliveryReceivers
{
    public const string DeliverEmailChallenge = nameof(DeliverEmailChallenge);
    public const string DeliverRecoveryChallenge = nameof(DeliverRecoveryChallenge);
}
