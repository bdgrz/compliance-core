using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Resolves the canonical complete program-creation grant path from trusted source.</summary>
/// <remarks>
/// A qualified resolver binds every participant to the exact policy-defined owner,
/// authenticated user and tenant. Its immutable observation must include the
/// complete relevant alternative-grant and veto routing state. AlternativesResolved
/// may be true only when a denial of the selected path cannot hide another valid
/// organization-wide ProgramManage path. IsCurrentAsync must qualify the same
/// non-reused routing lineage through the complete candidate capture, including
/// alternatives and veto routes; projections, caller inputs and a quiet page do
/// not establish completeness. A missing or unqualified resolution is unavailable.
/// No default production implementation grants this qualification.
/// </remarks>
interface ITrustedAdmissionProgramPathResolver
{
    bool IsQualified { get; }

    ValueTask<ProgramAdmissionPath?> ResolveAsync(AccessAdmissionSemanticEvaluator.Credential actor,
        Uuid tenantId, CancellationToken ct);

    ValueTask<bool> IsCurrentAsync(ProgramAdmissionPath path, CancellationToken ct);
}
