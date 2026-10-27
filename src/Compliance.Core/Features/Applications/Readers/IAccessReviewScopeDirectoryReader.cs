using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Readers;

public interface IAccessReviewScopeDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
    ValueTask<AccessReviewScopeRecord?> GetAsync(Uuid tenantId, Uuid systemInstanceId,
        CancellationToken ct = default);
}
