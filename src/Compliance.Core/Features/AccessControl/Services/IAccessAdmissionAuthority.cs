using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
/// Admits an exact human operation from qualified current source authority.
/// Implementations must not accept admission evidence from the caller.
/// </summary>
public interface IAccessAdmissionAuthority
{
    /// <summary>
    /// Admits one platform-operator operation. A null result means definitive
    /// denial; an unavailable or unqualified source must fail as unavailable,
    /// not produce evidence or misrepresent the source as a definitive denial.
    /// </summary>
    ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext context,
        Uuid operationId, string target, string intentSha256, CancellationToken ct = default);

    /// <summary>
    /// Admits program creation against one complete organization-wide grant path.
    /// A null result means definitive denial; an unavailable or unqualified
    /// source must fail as unavailable, not produce admission evidence.
    /// </summary>
    ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, string target, string intentSha256,
        CancellationToken ct = default);

    /// <summary>
    /// Admits an edition selection on an existing program under its current
    /// applicable scope. A null result is a definitive denial; an unavailable
    /// or unqualified source must not produce evidence.
    /// </summary>
    ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, Uuid operationId, string target, string intentSha256,
        CancellationToken ct = default);
}
