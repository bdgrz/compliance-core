using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Starts the tenant's surrender of its confirmed slug.
/// </summary>
public sealed class RequestTenantSlugSurrenderHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<RequestTenantSlugSurrender>
{
    const string CommandKind = "RequestTenantSlugSurrender";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<RequestTenantSlugSurrender> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant is required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null, selected => new Tenant(request.TenantId, selected),
            tenant => tenant.Acceptance, tenant => tenant.PrepareRequestSlugSurrender(request.Slug),
            (Tenant tenant, OperationProvenance operation, ImmutableContentRef response) => tenant.RequestSlugSurrender(operation, response, request.Slug));
    }
}
