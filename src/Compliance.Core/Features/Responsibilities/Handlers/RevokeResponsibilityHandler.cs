using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
namespace Bdgrz.Compliance.Features.Responsibilities.Handlers;

public sealed class RevokeResponsibilityHandler(IResponsibilityScopeValidator scopeValidator,
    ControlOperations controls, CommitmentOperations commitments, BoundaryOperations boundaries) : IRequestHandler<RevokeResponsibility>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevokeResponsibility> context, CancellationToken ct) => context.Request.RecordType switch
    {
        SeparationOfDutiesRecordTypes.Control => controls.RevokeResponsibilityAsync(context, scopeValidator, ct),
        SeparationOfDutiesRecordTypes.Commitment => commitments.RevokeResponsibilityAsync(context, scopeValidator, ct),
        SeparationOfDutiesRecordTypes.Boundary => boundaries.RevokeResponsibilityAsync(context, scopeValidator, ct),
        _ => ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation, "The responsibility record type is not supported."))),
    };
}
