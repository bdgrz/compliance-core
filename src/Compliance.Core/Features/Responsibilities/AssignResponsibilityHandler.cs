using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed class AssignResponsibilityHandler(ITenantMembershipDirectoryReader memberships,
    IResponsibilityScopeValidator scopeValidator, ControlOperations controls, CommitmentOperations commitments, BoundaryOperations boundaries)
    : IRequestHandler<AssignResponsibility>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssignResponsibility> context, CancellationToken ct) => context.Request.RecordType switch
    {
        SeparationOfDutiesRecordTypes.Control => controls.AssignResponsibilityAsync(context, memberships, scopeValidator, ct),
        SeparationOfDutiesRecordTypes.Commitment => commitments.AssignResponsibilityAsync(context, memberships, scopeValidator, ct),
        SeparationOfDutiesRecordTypes.Boundary => boundaries.AssignResponsibilityAsync(context, memberships, scopeValidator, ct),
        _ => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation, "The responsibility record type is not supported."))),
    };
}
