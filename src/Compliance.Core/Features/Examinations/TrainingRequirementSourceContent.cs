using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record TrainingRequirementSourceContent(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, string Identifier, long Version, TrainingRequirementContent Content,
    string ContentSha256, string Cadence, ActorReference ChangedBy, DateTimeOffset ChangedAt);
