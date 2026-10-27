using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record TrainingRequirementSourceContent(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, string Identifier, long Version, TrainingRequirementContent Content,
    string ContentSha256, string Cadence, ActorReference ChangedBy, DateTimeOffset ChangedAt);
