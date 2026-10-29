using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Deterministic member registrations dispatched by internal reactors, so replays reuse one logical id.</summary>
static class MemberRegistrationEffects
{
    /// <summary>The registration command a source event causes for one tenant user.</summary>
    public static RegisterMember Register(Uuid sourceEventId, Uuid tenantId, Uuid userId,
        string affiliation = "client_personnel", DateOnly? membershipExpiresOn = null) =>
        new(CommandId(sourceEventId, tenantId, userId), tenantId, userId, affiliation, membershipExpiresOn);

    /// <summary>The logical registration id, which is also the membership episode id.</summary>
    public static Uuid CommandId(Uuid sourceEventId, Uuid tenantId, Uuid userId) =>
        EffectIdentity.CommandId(sourceEventId, "member:register", $"{tenantId}:{userId}", null);
}
