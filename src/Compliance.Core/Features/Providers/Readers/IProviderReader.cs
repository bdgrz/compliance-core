using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Readers;

public interface IProviderReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<ProviderView?> GetAsync(Uuid tenantId, Uuid providerId, CancellationToken ct = default);
    ValueTask<ProviderView?> GetRevisionAsync(Uuid tenantId, Uuid providerId, long revision, CancellationToken ct = default);
    ValueTask<Page<ProviderView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default);
    ValueTask<Page<ProviderView>> ListRevisionsAsync(Uuid tenantId, Uuid providerId, int limit, string? cursor, CancellationToken ct = default);
}
