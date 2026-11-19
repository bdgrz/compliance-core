using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class LinkPersonToMemberHandler(PersonMemberLinks links) : IRequestHandler<LinkPersonToMember, UnitResult>
{
    public ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<LinkPersonToMember> context, CancellationToken ct) =>
        links.LinkAsync(context, ct);
}
