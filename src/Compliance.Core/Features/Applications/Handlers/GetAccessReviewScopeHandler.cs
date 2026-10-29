using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>Reads the effective scope decision and its retained history from the source stream.</summary>
public sealed class GetAccessReviewScopeHandler(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content,
    TimeProvider clock, RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessReviewScope, AccessReviewScopeView>
{
    public async ValueTask<Result<AccessReviewScopeView>> HandleAsync(
        IRequestContext<GetAccessReviewScope> context, CancellationToken ct)
    {
        var request = context.Request;
        var instance = await ScopedSystemInstanceSource.FindAsync(reader, events,
            request.TenantId, request.ApplicationId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        if (instance is null)
            return Result<AccessReviewScopeView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false))
            return Result<AccessReviewScopeView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        try
        {
            var decisions = await AccessReviewScopeReader.ReadAsync(reader, events, content, request.TenantId,
                request.ApplicationId, request.SystemInstanceId, ct).ConfigureAwait(false);
            return Result<AccessReviewScopeView>.Success(AccessReviewScopeReader.View(request.TenantId,
                request.ApplicationId, request.SystemInstanceId, decisions, request.AsOf ?? clock.GetUtcNow()));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<AccessReviewScopeView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact scope decision source cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
