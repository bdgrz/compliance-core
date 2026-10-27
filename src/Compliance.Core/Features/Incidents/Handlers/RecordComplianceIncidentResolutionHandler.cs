using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Incidents.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Commands;
namespace Bdgrz.Compliance.Features.Incidents.Handlers;

public sealed class RecordComplianceIncidentResolutionHandler(IncidentOperations operations, FollowUpCompletionVerifier completions)
    : IRequestHandler<RecordComplianceIncidentResolution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncidentResolution> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Incident resolution requires a current Bdgrz member.")));
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.IncidentId, request.CommandId,
            nameof(RecordComplianceIncidentResolution), request.ExpectedRevision, ActorReference.ForMember(member, display), async (snapshot, operation, token) =>
            {
                if (request.Content is null)
                    return Result.Failure(new RequestError(RequestErrorKind.Validation, "The resolution is required."));
                var verified = await completions.VerifyAsync(request.TenantId, request.ProgramId, request.Content, token).ConfigureAwait(false);
                return !verified.IsSuccess ? Result.Failure(verified.Error!) : IncidentOperations.Outcome(snapshot.Rules.Resolve(request.ExpectedRevision,
                    verified.Value, member, display, operation.AcceptedAt));
            }, ct);
    }
}
