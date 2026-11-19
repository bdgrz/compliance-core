using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Resolves one complete exact-scope program-management path for an existing program.</summary>
/// <remarks>
/// A qualified resolver must bind the user, tenant, program and chosen participant owners;
/// its non-reused routing generation must cover all applicable organization and exact-program
/// grant alternatives and the complete bounded veto route. AlternativesResolved may be true
/// only when denial of the selected path cannot conceal any other currently valid organization
/// or exact-program path. IsCurrentAsync rechecks the same immutable route,
/// scope, alternatives and veto generation through the complete source capture. A projection,
/// an empty page or caller-supplied routing is not qualification. No default provider grants it.
/// </remarks>
interface ITrustedAdmissionProgramSelectionPathResolver
{
    bool IsQualified { get; }

    ValueTask<ProgramSelectionAdmissionPath?> ResolveAsync(AccessAdmissionSemanticEvaluator.Credential actor,
        Uuid tenantId, Uuid programId, CancellationToken ct);

    ValueTask<bool> IsCurrentAsync(ProgramSelectionAdmissionPath path, CancellationToken ct);
}
