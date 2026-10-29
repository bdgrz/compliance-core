using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignCompletionView(Uuid CompletionId, Uuid PersonId,
    long RequirementVersion, DateOnly CompletedOn, string Source, string EvidenceReference,
    ActorReference Recorder, DateTimeOffset RecordedAt);
