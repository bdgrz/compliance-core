using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Aggregates;
using Bdgrz.Compliance.Features.FirmServices.Commands;
using Bdgrz.Compliance.Features.FirmServices.Models;
using Bdgrz.Compliance.Features.FirmServices.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Handlers;

/// <summary>Records one explicit operator duty decision; client role grants confer no such authority.</summary>
sealed class DesignatePartnerHandler(IAggregateExecutor executor, FirmServicesAuthority authority, TimeProvider clock)
    : IRequestHandler<DesignatePartner, PartnerDesignationView>
{
    public async ValueTask<Result<PartnerDesignationView>> HandleAsync(IRequestContext<DesignatePartner> context,
        CancellationToken ct)
    {
        var actor = await authority.RequireOperatorAsync(context.Actor, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result<PartnerDesignationView>.Failure(actor.Error!);
        var request = context.Request;
        if (!await authority.IsActiveStaffAsync(request.UserId, ct).ConfigureAwait(false))
            return Result<PartnerDesignationView>.Failure(new(RequestErrorKind.Conflict,
                "Only active firm staff can receive a partner designation."));
        var at = clock.GetUtcNow();
        return await executor.ExecuteAsync(new PartnerDesignation(request.UserId, request.Duty, request.CommandId),
            owner =>
            {
                var result = owner.Designate(request, actor.Value, at);
                return result.IsSuccess ? AggregateOutcome.Commit(result) : AggregateOutcome.Discard(result);
            }, context, ct).ConfigureAwait(false);
    }
}
