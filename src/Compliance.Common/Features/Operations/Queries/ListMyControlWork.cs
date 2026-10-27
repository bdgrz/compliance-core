using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Queries;

/// <summary>Lists the acting member's operating responsibilities and due or upcoming work.</summary>
[Discriminator("bdgrz.control.work.mine", 1)]
public sealed record ListMyControlWork(Uuid TenantId, Uuid ProgramId, int? HorizonDays = null)
    : IRequest<MyControlWorkView>, IProgramReadRequest, ICallable;
