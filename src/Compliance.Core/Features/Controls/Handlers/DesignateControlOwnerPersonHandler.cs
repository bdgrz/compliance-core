using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class DesignateControlOwnerPersonHandler(ControlOperations operations, IAggregateReader reader,
    ControlLifecycleReleaseGate releaseGate) : IRequestHandler<DesignateControlOwnerPerson>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DesignateControlOwnerPerson> context, CancellationToken ct)
    {
        var request = context.Request;
        Uuid? correlatedMemberId = null;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(DesignateControlOwnerPerson), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (!releaseGate.IsEnabled)
                    return Result.Failure(ControlLifecycleReleaseGate.Unavailable);
                if (request.PersonId is not { } personId)
                    return Result.Success;
                var person = await reader.HydrateAsync(new Person(request.TenantId, personId), token).ConfigureAwait(false);
                if (!person.IsCreated)
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "The control owner must be a recorded workforce person in this organization."));
                correlatedMemberId = person.CorrelatedUserId is { } user ? RbacIds.Member(request.TenantId, user) : null;
                return Result.Success;
            },
            (rules, actor, at) => ControlOperations.Outcome(rules.DesignatePersonOwner(request.ProgramId,
                request.ExpectedRevision, request.CommandId, request.PersonId, correlatedMemberId, request.Rationale,
                actor.MemberId, actor.Snapshot, at)),
            (rules, actor, at) => new ControlOwnerPersonDesignatedContent(request.TenantId, request.ProgramId,
                request.ControlId, rules.DraftVersionId, rules.Revision, request.CommandId, request.PersonId,
                correlatedMemberId, request.Rationale.Trim(), actor.MemberId, actor.Snapshot, at), null, ct);
    }
}
