using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetControlDraftOwnerPersonHandler(ControlActivationSource source)
    : IRequestHandler<GetControlDraftOwnerPerson, ControlDraftOwnerPersonView>
{
    public async ValueTask<Result<ControlDraftOwnerPersonView>> HandleAsync(
        IRequestContext<GetControlDraftOwnerPerson> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ControlDraftOwnerPersonView>.Failure(loaded.Error);
        var control = loaded.Value;
        return Result<ControlDraftOwnerPersonView>.Success(new ControlDraftOwnerPersonView(
            control.TenantId, control.ProgramId, control.Id,
            control.HasOpenDraft ? control.DraftVersionId : null, control.Revision,
            control.CurrentPersonOwnerDesignation));
    }
}
