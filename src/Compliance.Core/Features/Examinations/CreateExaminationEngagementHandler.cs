using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed class CreateExaminationEngagementHandler(ExaminationEngagementOperations operations,
    IAggregateReader reader, ReadinessReader readiness) : IRequestHandler<CreateExaminationEngagement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<CreateExaminationEngagement> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ExaminationId,
            request.ExaminationId, nameof(CreateExaminationEngagement), null, owner => owner.PrepareCreate(request.ProgramId),
            async (snapshot, operation, token) =>
            {
                var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, token).ConfigureAwait(false);
                var entries = await readiness.ReadEntryDecisionsAsync(request.TenantId, program, token).ConfigureAwait(false);
                if (entries.Count == 0 || entries[0].Outcome is not (ProgramReadiness.Approve or ProgramReadiness.ApproveWithExceptions))
                    return Result<ExaminationEngagementState>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "A Type I engagement requires an approved Type I entry decision."));
                var contacts = request.ManagementContactMemberIds ?? [];
                var invalid = await ExaminationManagementContacts.ValidateAsync(reader, request.TenantId, contacts, token)
                    .ConfigureAwait(false);
                if (invalid is not null)
                    return Result<ExaminationEngagementState>.Failure(invalid);
                var terms = new ExaminationEngagementContent(request.EngagementType, request.AuditFirm, request.PeriodStart,
                    request.PeriodEnd, request.TrustServicesCategories, request.BoundaryId, contacts);
                if (snapshot.Rules.Create(terms, ExaminationEngagement.Member(operation), operation.Actor.Display,
                        operation.AcceptedAt) is { } failure)
                    return ExaminationEngagementOperations.Failure(failure);
                return ExaminationEngagementOperations.FromRules(snapshot.Rules);
            }, ct);
    }
}
