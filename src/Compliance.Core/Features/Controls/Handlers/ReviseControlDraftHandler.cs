using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ReviseControlDraftHandler(ControlOperations operations,
    IControlApplicabilityReferenceValidator applicability) : IRequestHandler<ReviseControlDraft>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseControlDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(ReviseControlDraft), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (ControlDraft.ValidateContent(request.Content) is { } structural)
                    return Result.Failure(structural);
                return snapshot.Rules.HasSameCurrentContent(request.Content) ? Result.Success
                    : await applicability.ValidateAsync(request.TenantId, request.Content, token).ConfigureAwait(false);
            },
            (rules, actor, at) => ControlOperations.Outcome(rules.Revise(request.ProgramId, request.ExpectedRevision,
                request.Content, actor.MemberId, actor.Display, at)),
            (rules, actor, at) => new ControlDraftRevisedContent(request.TenantId, request.ProgramId, request.ControlId,
                rules.Revision, ControlRules.NormalizeContent(request.Content), actor.MemberId, actor.Display, at)
            { StoredActor = actor.Snapshot }, null, ct);
    }
}
