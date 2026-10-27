using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

/// <summary>The existing internal process request; no public lookup, delivery ticket or new trusted callback is introduced.</summary>
public sealed class GenerateSystemDescriptionExportHandler(SystemDescriptionExportProcess process) : IRequestHandler<GenerateSystemDescriptionExport>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GenerateSystemDescriptionExport> context, CancellationToken ct) => process.ExecuteAsync(context, ct);
}
