using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
namespace Bdgrz.Compliance.Features.AuditorRequests.Projections;

public interface IAuditorRequestDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(AuditorRequestRecorded fact, CancellationToken ct = default);
    ValueTask ApplyAsync(AuditorSampleSelectionAdmitted fact, CancellationToken ct = default);
}
