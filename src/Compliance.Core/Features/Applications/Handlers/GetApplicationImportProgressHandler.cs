using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class GetApplicationImportProgressHandler(IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<GetApplicationImportProgress, ApplicationImportProgress>
{
    public async ValueTask<Result<ApplicationImportProgress>> HandleAsync(IRequestContext<GetApplicationImportProgress> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportLifecycleRead.LoadAsync(reader, request.TenantId,
            request.BatchId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportProgress>.Failure(loaded.Error);
        var batch = loaded.Value;
        var planned = 0;
        var durable = 0;
        if (batch.Plan is { } sealedPlan)
        {
            try
            {
                var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, request.TenantId,
                    sealedPlan.FrozenPlan, ct).ConfigureAwait(false);
                var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(content,
                    request.TenantId, plan.TargetGroups, ct).ConfigureAwait(false);
                planned = checked((int)plan.Rows.Count);
                foreach (var group in groups)
                {
                    var rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(content,
                        request.TenantId, group.Rows, ct).ConfigureAwait(false);
                    var target = await reader.HydrateAsync(new DeclaredApplication(request.TenantId, group.ApplicationId), ct)
                        .ConfigureAwait(false);
                    foreach (var row in rows)
                    {
                        var effect = target.GetPendingImportEffect(batch.Id, row.RowId);
                        if (effect is null)
                            continue;
                        if (target.Stream.Realm != request.TenantId.ToString() || target.Id != group.ApplicationId ||
                            effect.FrozenPlan != sealedPlan.FrozenPlan || effect.Row != row)
                            return Result<ApplicationImportProgress>.Failure(new RequestError(RequestErrorKind.Conflict,
                                "The target progress differs from its frozen import plan."));
                        if (target.CommittedStreamPosition >= effect.Metadata.AggregateVersion)
                            durable++;
                    }
                }
            }
            catch (ImmutableContentIntegrityException)
            {
                return Result<ApplicationImportProgress>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The frozen import plan cannot be verified yet.", isTransient: true));
            }
        }

        var fence = await ApplicationImportLifecycleRead.CheckAsync(reader, batch, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ApplicationImportProgress>.Failure(fence.Error);
        return Result<ApplicationImportProgress>.Success(new(request.TenantId, batch.Id, batch.Revision,
            batch.State, planned, durable, batch.Committed is null ? 0 : planned, batch.Failed?.FailureCode,
            batch.Failed?.RowId, batch.Plan?.Operation.AcceptedAt, batch.Committed?.CommittedAt));
    }
}
