using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class CreateExaminationEngagementHandler(IAggregateReader reader,
    IAggregateExecutor executor, ReadinessReader readiness, TimeProvider clock)
    : IRequestHandler<CreateExaminationEngagement>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateExaminationEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationManagementActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var memberId, out var display))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "An examination engagement requires a personal HTTP actor."));
        IReadOnlyList<TypeIEntryDecisionView> entryDecisions;
        try
        {
            var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct)
                .ConfigureAwait(false);
            entryDecisions = await readiness.ReadEntryDecisionsAsync(request.TenantId, program, ct)
                .ConfigureAwait(false);
        }
        catch (Content.ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The Type I entry decision cannot be verified yet: {exception.Message}", true));
        }

        if (entryDecisions.Count == 0 || entryDecisions[0].Outcome is not
            (ProgramReadiness.Approve or ProgramReadiness.ApproveWithExceptions))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A Type I engagement requires an approved Type I entry decision."));
        var contacts = request.ManagementContactMemberIds ?? [];
        var validation = await ExaminationManagementContacts.ValidateAsync(reader,
            request.TenantId, contacts, ct).ConfigureAwait(false);
        if (validation is not null)
            return Result.Failure(validation);
        var content = new ExaminationEngagementContent(request.EngagementType, request.AuditFirm,
            request.PeriodStart, request.PeriodEnd, request.TrustServicesCategories,
            request.BoundaryId, contacts);
        return await executor.ExecuteAsync(new ExaminationEngagement(request.TenantId,
                request.ProgramId, request.ExaminationId), engagement =>
                CommandFailureRequestAdapter.ToOutcome(
                    engagement.Create(content, memberId, display, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
