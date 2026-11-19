using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Hosting;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class RecordServiceIdentityHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RecordServiceIdentity, PortiaCreateOutcome<ServiceIdentityRegistration>>
{
    public async ValueTask<Result<PortiaCreateOutcome<ServiceIdentityRegistration>>> HandleAsync(
        IRequestContext<RecordServiceIdentity> context, CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var invalid = await ServiceIdentityOwners.ValidateAsync(reader, request.TenantId,
            request.OwnerKind, request.OwnerId, ct).ConfigureAwait(false);
        if (invalid is not null)
            return Result<PortiaCreateOutcome<ServiceIdentityRegistration>>.Failure(invalid);
        var now = clock.GetUtcNow();
        var terms = new ServiceIdentityTerms(request.DisplayName, request.IdentityKind,
            request.Purpose, request.Environment, "active", request.OwnerKind, request.OwnerId,
            request.ReviewBy, request.ExpiresOn, request.SharedJustification);
        var created = false;
        var result = await executor.ExecuteAsync(new ServiceIdentity(request.TenantId, context.RequestId),
            identity =>
            {
                created = !identity.IsCreated;
                return AggregateOutcome.CommitOnSuccess(identity.Record(terms,
                    DateOnly.FromDateTime(now.UtcDateTime), actor, now));
            }, context, ct).ConfigureAwait(false);
        return PortiaCreateOutcomes.From(result, created);
    }
}
