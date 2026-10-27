using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Projections;

public interface IApplicationImportDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default);

    /// <summary>Projects the staged observation with its complete rows, read from the immutable rows manifest.</summary>
    ValueTask ApplyStagedAsync(ApplicationImportStaged staged, IReadOnlyList<ApplicationImportStagedRow> rows,
        CancellationToken ct = default);
}
