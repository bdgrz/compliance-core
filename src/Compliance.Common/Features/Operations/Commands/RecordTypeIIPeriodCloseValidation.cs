using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Commands;

/// <summary>Records the server-reconciled exact source population at the completed observation interval.</summary>
[Discriminator("bdgrz.operations.record-type-ii-period-close-validation", 1)]
public sealed record RecordTypeIIPeriodCloseValidation(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid PeriodId, long ExpectedRevision) : IRequest<UnitResult>, IIdentifiedRequest, IProgramScopedRequest,
    IClientManagementMutationRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
