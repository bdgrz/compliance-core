using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>The existing internal process request; no public lookup, delivery ticket or new trusted callback is introduced.</summary>
public sealed class GenerateSystemDescriptionExportHandler(SystemDescriptionExportProcess process) : IRequestHandler<GenerateSystemDescriptionExport>
{
    public ValueTask<Result> HandleAsync(IRequestContext<GenerateSystemDescriptionExport> context, CancellationToken ct) => process.ExecuteAsync(context, ct);
}
