using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationReadinessImpact(
    IReadOnlyList<ApplicationReadinessReferenceView> References, bool Complete,
    bool ProgramsOverLimit, bool ReferencesOverLimit);

public interface IApplicationReadinessImpactReader
{
    ValueTask<Result<ApplicationReadinessImpact>> ReadAsync(Uuid tenantId, Uuid applicationId,
        IReadOnlyCollection<Uuid> systemInstanceIds, bool systemInstanceListComplete,
        CancellationToken ct);
}
