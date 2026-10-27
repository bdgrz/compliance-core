using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Resolves current source content for a source already selected in a handoff.</summary>
public interface IAdvisorSharedSourceReader
{
    ValueTask<Result<AdvisorSharedSourceView>> GetAsync(Uuid tenantId, Uuid programId,
        Uuid handoffId, AdvisorRecordVersion selectedSource, CancellationToken ct = default);
}
