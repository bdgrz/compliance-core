using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks the active advisor assignment or guest grant for an exact handoff source.</summary>
public interface IAdvisorSharedSourceAuthorization
{
    ValueTask<bool> CanReadSelectedSourceAsync(Uuid tenantId, Uuid programId, Uuid handoffId,
        AdvisorRecordVersion sourceVersion, Uuid userId, CancellationToken ct = default);
}
