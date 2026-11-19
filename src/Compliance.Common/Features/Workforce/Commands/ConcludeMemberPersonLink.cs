using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.person.member.conclude", 1)]
public sealed record ConcludeMemberPersonLink(Uuid CommandId, Uuid TenantId, NativeFactReference Source,
    ulong SourcePosition, bool Released) : IRequest, IWorkforceRequest;
