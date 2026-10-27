using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A guard fact that re-read and verified against its proof, with the frozen intent it names.</summary>
public sealed record GuardAdmissionProof(ITenantManagerGuardFact Fact, AdministratorTransitionIntent Intent,
    Uuid FactEventId);
