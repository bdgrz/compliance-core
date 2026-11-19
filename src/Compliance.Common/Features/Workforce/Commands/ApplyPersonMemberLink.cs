using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Commands;

[Discriminator("bdgrz.workforce.person.member.apply-admission", 1)]
public sealed record ApplyPersonMemberLink(Uuid CommandId, Uuid TenantId, Uuid PersonId, NativeFactReference Source,
    ulong SourcePosition) : IRequest, IWorkforceRequest;
