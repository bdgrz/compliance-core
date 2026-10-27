using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

/// <summary>Reads a roster snapshot from its immutable source and redacts restricted fields.</summary>
static class WorkforceRosterSnapshotReads
{
    public static async ValueTask<Result<WorkforceRosterSnapshotView>> ReadAsync<TRequest>(
        IRequestContext<TRequest> context, IAggregateReader reader,
        IPermissionAuthorizer permissions, Uuid tenantId, Uuid snapshotId, CancellationToken ct)
        where TRequest : IRequestBase
    {
        var content = await PopulationSnapshotContent.ReadAsync(reader, tenantId, snapshotId,
            WorkforceRosterSnapshotContent.Kind, ct).ConfigureAwait(false);
        if (!content.IsSuccess)
            return Result<WorkforceRosterSnapshotView>.Failure(new RequestError(content.Error.Kind,
                content.Error.Kind == RequestErrorKind.NotFound
                    ? "The roster snapshot was not found."
                    : "The stored roster snapshot failed integrity verification."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var managerChain = await FieldRestrictions.ForActorAsync(permissions, tenantId, userId,
            FieldClasses.WorkforceManagerChain, ct).ConfigureAwait(false);
        return Result<WorkforceRosterSnapshotView>.Success(
            WorkforceRosterSnapshotContent.ToView(tenantId, content.Value.Snapshot,
                content.Value.Rows, managerChain));
    }
}
