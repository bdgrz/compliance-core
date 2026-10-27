using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationImportDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent ev, CancellationToken ct = default);

    /// <summary>Projects the staged observation with its complete rows, read from the immutable rows manifest.</summary>
    ValueTask ApplyStagedAsync(ApplicationImportStaged staged, IReadOnlyList<ApplicationImportStagedRow> rows,
        CancellationToken ct = default);
}
