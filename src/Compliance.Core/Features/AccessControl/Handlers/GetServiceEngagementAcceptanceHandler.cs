using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Reads the current accepted snapshot of an engagement from its own owner.</summary>
public sealed class GetServiceEngagementAcceptanceHandler(IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<GetServiceEngagementAcceptance, ServiceEngagementAcceptanceView>
{
    public async ValueTask<Result<ServiceEngagementAcceptanceView>> HandleAsync(
        IRequestContext<GetServiceEngagementAcceptance> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty)
            return NotFound();
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(request.TenantId, request.EngagementId),
            ct).ConfigureAwait(false);
        return owner.AcceptanceResult is { } result
            ? Result<ServiceEngagementAcceptanceView>.Success(await IndependenceContent
                .ReadResultAsync<ServiceEngagementAcceptanceView>(content, IndependenceContent.Realm(request.TenantId),
                    result, ct).ConfigureAwait(false))
            : NotFound();
    }

    static Result<ServiceEngagementAcceptanceView> NotFound() => Result<ServiceEngagementAcceptanceView>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The accepted engagement was not found."));
}
