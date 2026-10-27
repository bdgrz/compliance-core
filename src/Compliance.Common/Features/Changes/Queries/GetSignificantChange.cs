using Cntryl.Portia;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Changes.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.changes.get-significant-change", 2)]
public sealed record GetSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId)
    : IRequest<SignificantChangeView>, IProgramReadRequest, ICallable;
