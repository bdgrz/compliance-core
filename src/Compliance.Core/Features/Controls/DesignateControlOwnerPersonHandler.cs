using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

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
