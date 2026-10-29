using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Rules;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content,
        CancellationToken ct = default);
}
