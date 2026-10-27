using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;

/// <summary>Verifies each selected source version against its current owning feature.</summary>
public interface IAdvisorRecordVersionResolver
{
    ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default);
}
