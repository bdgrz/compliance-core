using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Applications.Readers;
namespace Bdgrz.Compliance.Features.AccessReviews.Handlers;

public sealed class ListAccessExpectationsHandler(AccessExpectationsReader expectations,
    IApplicationDirectoryReader applications, RestrictedApplicationVisibility visibility)
    : IRequestHandler<ListAccessExpectations, AccessExpectationsView>
{
    public async ValueTask<Result<AccessExpectationsView>> HandleAsync(
        IRequestContext<ListAccessExpectations> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await applications.GetInstanceAsync(request.TenantId,
            request.SystemInstanceId, ct).ConfigureAwait(false);
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (instance is null || !await visibility.CanReadSystemInstanceAsync(request.TenantId,
                userId, instance.ApplicationId, request.SystemInstanceId, ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessExpectationsView>(RequestErrorKind.NotFound,
                "The system instance was not found.");
        var ledger = await expectations.ReadAsync(request.TenantId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        return Result<AccessExpectationsView>.Success(ledger.ToView());
    }
}
