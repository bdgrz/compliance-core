using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class UnlinkPersonFromMemberHandler(PersonMemberLinks links) : IRequestHandler<UnlinkPersonFromMember, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<UnlinkPersonFromMember> context, CancellationToken ct) =>
        links.UnlinkAsync(context, ct);
}
