using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;

/// <summary>Verifies each selected source version against its current owning feature.</summary>
public interface IAdvisorRecordVersionResolver
{
    /// <summary>Checks current eligibility when admitting new handoff reliance.</summary>
    ValueTask<Result> RequireCurrentAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default);

    /// <summary>Checks an already selected exact source without revoking retained support history.</summary>
    ValueTask<Result> RequireRetainedAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<AdvisorRecordVersion> selection, CancellationToken ct = default) =>
        RequireCurrentAsync(tenantId, programId, selection, ct);
}
