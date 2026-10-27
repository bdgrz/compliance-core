using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Changes.Services;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Queries;
using Bdgrz.Compliance.Features.Changes.Readers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.Changes.Handlers;

public sealed class GetSignificantChangeHandler(SignificantChangeReader changes,
    RestrictedComplianceRecordVisibility visibility)
    : IRequestHandler<GetSignificantChange, SignificantChangeView>
{
    public async ValueTask<Result<SignificantChangeView>> HandleAsync(
        IRequestContext<GetSignificantChange> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.ChangeId == Uuid.Empty)
            return Result<SignificantChangeView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Tenant, program, and significant-change identities are required."));

        try
        {
            var change = (await changes.ReadAsync(request.TenantId, request.ProgramId, request.ChangeId, ct).ConfigureAwait(false)).View;
            return change is { Current: { } current } view && view.TenantId == request.TenantId &&
                   view.ProgramId == request.ProgramId && view.SignificantChangeId == request.ChangeId &&
                   await visibility.CanReadAsync(request.TenantId, context.Actor,
                       current.Content.HandlingClass,
                       view.Assessments.Select(static assessment => assessment.AssessedByMemberId).ToArray(), ct)
                       .ConfigureAwait(false)
                ? Result<SignificantChangeView>.Success(view)
                : Result<SignificantChangeView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The significant change was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<SignificantChangeView>.Failure(SignificantChangeOperations.ContentFailure(exception));
        }
    }
}
