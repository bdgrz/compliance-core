using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignAcknowledgementView(Uuid AcknowledgementId, Uuid PersonId,
    long PolicyVersion, string ContentSha256, string AcknowledgementText,
    ActorReference Performer, ActorReference Recorder, bool RecordedOnBehalf,
    DateTimeOffset AcknowledgedAt);
