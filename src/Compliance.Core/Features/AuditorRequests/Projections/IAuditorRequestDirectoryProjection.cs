using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

public interface IAuditorRequestDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(AuditorRequestRecorded fact, CancellationToken ct = default);
    ValueTask ApplyAsync(AuditorSampleSelectionAdmitted fact, CancellationToken ct = default);
}
