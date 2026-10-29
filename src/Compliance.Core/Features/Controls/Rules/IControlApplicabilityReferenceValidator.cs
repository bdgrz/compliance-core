using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
namespace Bdgrz.Compliance.Features.Controls.Rules;

public interface IControlApplicabilityReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
        CancellationToken ct = default);
}
