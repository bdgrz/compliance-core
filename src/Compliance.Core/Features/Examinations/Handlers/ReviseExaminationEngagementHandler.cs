using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

sealed class ReviseExaminationEngagementHandler(ExaminationEngagementOperations operations, IAggregateReader reader)
    : IRequestHandler<ReviseExaminationEngagement>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseExaminationEngagement> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.ExaminationId,
            request.CommandId, nameof(ReviseExaminationEngagement), request.ExpectedRevision,
            owner => owner.PrepareWrite(request.ProgramId, request.ExpectedRevision, requireFrozen: false),
            async (snapshot, operation, token) =>
            {
                var current = snapshot.View!;
                var contacts = request.ManagementContactMemberIds ?? current.ManagementContactMemberIds;
                var invalid = await ExaminationManagementContacts.ValidateAsync(reader, request.TenantId, contacts, token)
                    .ConfigureAwait(false);
                if (invalid is not null)
                    return Result<ExaminationEngagementState>.Failure(invalid);
                var terms = new ExaminationEngagementContent(current.EngagementType, request.AuditFirm, request.PeriodStart,
                    request.PeriodEnd, request.TrustServicesCategories, current.BoundaryId, contacts);
                if (snapshot.Rules.Revise(request.ExpectedRevision, terms, ExaminationEngagement.Member(operation),
                        operation.Actor.Display, operation.AcceptedAt) is { } failure)
                    return ExaminationEngagementOperations.Failure(failure);
                return ExaminationEngagementOperations.FromRules(snapshot.Rules);
            }, ct);
    }
}
