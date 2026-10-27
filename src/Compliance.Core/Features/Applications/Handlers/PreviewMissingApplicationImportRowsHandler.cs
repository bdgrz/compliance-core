using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class PreviewMissingApplicationImportRowsHandler(
    ApplicationImportReadConsistency consistency, IAggregateReader reader, IImmutableContentStore content,
    ApplicationPublicationReader publication, IDomainEventReader events)
    : IRequestHandler<PreviewMissingApplicationImportRows, Page<ApplicationImportMissingRow>>
{
    public async ValueTask<Result<Page<ApplicationImportMissingRow>>> HandleAsync(
        IRequestContext<PreviewMissingApplicationImportRows> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationImportMissingRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The missing-row preview limit must be between 1 and 200."));
        var unwrapped = ApplicationPublicationCursor.Unwrap(request.Cursor);
        if (!unwrapped.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(unwrapped.Error);
        var (carried, innerCursor) = unwrapped.Value;
        var fresh = await consistency.GetFreshAsync(request.TenantId, request.BatchId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(fresh.Error);
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, content, events, request.TenantId,
            request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(loaded.Error);
        var (batch, source, rows) = loaded.Value;
        if (batch.Revision != fresh.Value.Revision)
            return Result<Page<ApplicationImportMissingRow>>.Failure(Changed());
        var missing = source.Missing(batch, rows);
        var position = source.Source.CommittedStreamPosition;
        var decoded = ApplicationImportMissingPaging.Decode(request.TenantId, request.BatchId,
            fresh.Value.Revision, position, innerCursor);
        if (!decoded.IsSuccess)
            return Result<Page<ApplicationImportMissingRow>>.Failure(decoded.Error);
        var offset = decoded.Value;
        if (innerCursor is not null && offset >= missing.Count)
            return Result<Page<ApplicationImportMissingRow>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The missing-row preview cursor is outside its source claims."));
        var limit = request.Limit ?? 50;
        var items = new List<ApplicationImportMissingRow>();
        var cut = await publication.OpenAsync(carried, ct).ConfigureAwait(false);
        foreach (var claim in missing.Skip(offset).Take(limit))
        {
            var target = await cut.ReadAsync(request.TenantId, claim.Observation.ApplicationId, ct)
                .ConfigureAwait(false);
            var expected = claim.Observation.ExpectedApplicationRevision ?? 1;
            var blockers = new List<string> { "missing_source_retirement_unavailable", "retirement_impact_unavailable" };
            if (!target.IsCreated || target.IsRetired || target.Id != claim.Observation.ApplicationId ||
                target.Stream.Realm != request.TenantId.ToString() || target.Revision != expected)
                blockers.Add("source_claim_target_changed");
            if (batch.State is "canceled" or "canceling")
                blockers.Add("batch_canceled");
            items.Add(new ApplicationImportMissingRow(request.TenantId, request.BatchId,
                ApplicationImportEffects.SourceClaimId(source.Source.Id, claim.Observation.SourceRecordId),
                claim.Observation.SourceRecordId, claim.Observation.ApplicationId, claim.LastObservedBatchId,
                claim.Observation.Name, claim.Observation.Purpose, claim.Observation.OwnerReference,
                claim.CommittedAt, expected,
                target.IsCreated && target.Id == claim.Observation.ApplicationId && target.Stream.Realm == request.TenantId.ToString()
                    ? target.Revision : null,
                "missing_from_source", blockers));
        }
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, content, events, request.TenantId, batch, source, ct)
                .ConfigureAwait(false))
            return Result<Page<ApplicationImportMissingRow>>.Failure(Changed());
        var nextOffset = offset + items.Count;
        var nextCursor = nextOffset < missing.Count
            ? ApplicationPublicationCursor.Wrap(cut.Cut.GlobalWatermarkExclusive, ApplicationImportMissingPaging.Encode(
                request.TenantId, request.BatchId, fresh.Value.Revision, position, nextOffset))
            : null;
        return Result<Page<ApplicationImportMissingRow>>.Success(new(items, nextCursor));
    }

    static RequestError Changed() => new(RequestErrorKind.Conflict,
        "The import source changed while reading missing-row proposals.", isTransient: true);
}
