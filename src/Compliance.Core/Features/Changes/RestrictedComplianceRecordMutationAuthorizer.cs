using Bdgrz.Compliance.Features.Incidents;
using Cntryl.Portia;
using IncidentAggregate = Bdgrz.Compliance.Features.Incidents.ComplianceIncident;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Applies the T2-07 restricted handling rule to change and incident mutations.</summary>
sealed class RestrictedComplianceRecordMutationAuthorizer(
    IAggregateReader reader,
    RestrictedComplianceRecordVisibility visibility)
    : IRequestAuthorizer<IRestrictedComplianceRecordMutationRequest>
{
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IRestrictedComplianceRecordMutationRequest> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var target = request switch
        {
            RecordSignificantChange item => (IsChange: true, Id: item.ChangeId,
                ProposedHandlingClass: item.Content?.HandlingClass),
            ReviseSignificantChange item => (IsChange: true, Id: item.ChangeId,
                ProposedHandlingClass: item.Content?.HandlingClass),
            AssessSignificantChange item => (IsChange: true, Id: item.ChangeId,
                ProposedHandlingClass: (string?)null),
            RecordComplianceIncident item => (IsChange: false, Id: item.IncidentId,
                ProposedHandlingClass: item.Content?.HandlingClass),
            ReviseComplianceIncident item => (IsChange: false, Id: item.IncidentId,
                ProposedHandlingClass: item.Content?.HandlingClass),
            AssessComplianceIncident item => (IsChange: false, Id: item.IncidentId,
                ProposedHandlingClass: (string?)null),
            RecordComplianceIncidentResolution item => (IsChange: false, Id: item.IncidentId,
                ProposedHandlingClass: (string?)null),
            _ => (IsChange: false, Id: Uuid.Empty, ProposedHandlingClass: (string?)null),
        };

        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || target.Id == Uuid.Empty)
            return HiddenRecord();

        string? currentHandlingClass;
        Uuid[] currentAssessorIds;
        if (target.IsChange)
        {
            var aggregate = await reader.HydrateAsync(
                new SignificantChange(request.TenantId, request.ProgramId, target.Id), ct)
                .ConfigureAwait(false);
            var current = aggregate.Current;
            if (current is not null && (current.TenantId != request.TenantId ||
                current.ProgramId != request.ProgramId || current.SignificantChangeId != target.Id))
                return HiddenRecord();
            currentHandlingClass = current?.Current?.Content.HandlingClass;
            currentAssessorIds = current?.CurrentAssessment is { } assessment
                ? [assessment.AssessedByMemberId]
                : [];
        }
        else
        {
            var aggregate = await reader.HydrateAsync(
                new IncidentAggregate(request.TenantId, request.ProgramId, target.Id), ct)
                .ConfigureAwait(false);
            var current = aggregate.Current;
            if (current is not null && (current.TenantId != request.TenantId ||
                current.ProgramId != request.ProgramId || current.ComplianceIncidentId != target.Id))
                return HiddenRecord();
            currentHandlingClass = current?.Current?.Content.HandlingClass;
            currentAssessorIds = current?.CurrentAssessment is { } assessment
                ? [assessment.AssessedByMemberId]
                : [];
        }

        if (currentHandlingClass != "restricted" && target.ProposedHandlingClass != "restricted")
            return Result.Success;

        return await visibility.CanEditAsync(request.TenantId, context.Actor, "restricted",
                currentAssessorIds, ct).ConfigureAwait(false)
            ? Result.Success
            : HiddenRecord();
    }

    static Result HiddenRecord() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The compliance record was not found."));
}
