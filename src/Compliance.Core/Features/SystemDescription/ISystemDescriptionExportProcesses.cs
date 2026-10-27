using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public interface ISystemDescriptionExportProcesses
{
    Task<SystemDescriptionExportProcessRecord?> ReadAsync(Uuid tenant, Uuid processId, Uuid commandId, CancellationToken ct);
    Task<Result<SystemDescriptionExportProcessRecord>> AcceptIfAbsentAsync(SystemDescriptionExportProcessRecord candidate, CancellationToken ct);
    Task<Result<SystemDescriptionExportProcessRecord>> ReplaceAsync(SystemDescriptionExportProcessRecord expected, SystemDescriptionExportProcessRecord replacement, CancellationToken ct);
}
