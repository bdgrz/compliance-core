using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Verifies each selected source version against its current owning feature.</summary>
public interface IAdvisorRecordVersionResolver
{
    ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default);
}
