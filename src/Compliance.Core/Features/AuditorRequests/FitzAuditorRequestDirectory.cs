using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Projections;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class FitzAuditorRequestDirectory(IKvClient client, IImmutableContentStore content)
    : FitzKvProjectionStore(client, "kv://bdgrz/auditor-request-identity-directory/projection-v1", ProjectorName),
        IAuditorRequestDirectoryReader, IAuditorRequestDirectoryProjection
{
    public const string ProjectorName = "AuditorRequestIdentityDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AuditorRequest.Area)), ct);

    public async ValueTask ApplyAsync(AuditorRequestRecorded fact, CancellationToken ct = default)
    {
        if (fact.TenantId == Uuid.Empty || fact.SubjectId == Uuid.Empty || fact.Revision != 1 ||
            fact.Metadata is not { IsAudit: false, AggregateVersion: 1 } metadata || metadata.EventId == Uuid.Empty ||
            metadata.AggregateId != fact.SubjectId || fact.Change.Schema != AuditorContent.TermsSchema)
            throw Invalid("The request identity is not an exact native creation.");
        var terms = await content.ReadJsonAsync<AuditorRequestTermsContent>(fact.TenantId.ToString(), fact.Change, ct)
            .ConfigureAwait(false);
        if (terms.ProgramId == Uuid.Empty)
            throw Invalid("The request creation content has no program scope.");
        var owner = new AuditorRequest(fact.TenantId, terms.ProgramId, fact.SubjectId);
        if (terms.ExaminationId == Uuid.Empty || terms.At != fact.OccurredAt ||
            terms.At != fact.Operation.AcceptedAt || terms.ActorMemberId != fact.ActorMemberId ||
            fact.Operation.Actor.Kind != "member" || fact.Operation.Actor.Id != terms.ActorMemberId.ToString() ||
            fact.Operation.Actor.Display != terms.ActorDisplay ||
            !NativeOwnerHistory.IsOwnRoot(owner, fact.Operation, nameof(RecordAuditorRequest), null))
            throw Invalid("The request creation content has another original program, owner or attribution.");
        var identity = new AuditorRequestSummaryView(fact.TenantId, terms.ProgramId, fact.SubjectId,
            string.Empty, "created_identity", fact.OccurredAt);
        var existing = await AuditorRequestDirectorySchema.Requests.GetAsync(Transaction, fact.SubjectId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing != identity)
                throw Invalid("The request identity cannot change its original program or creation order.");
            return;
        }
        await AuditorRequestDirectorySchema.Requests.InsertAsync(Transaction, identity, ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyAsync(AuditorSampleSelectionAdmitted fact, CancellationToken ct = default)
    {
        if (!Uuid.TryParse(fact.Owner.Realm, CultureInfo.InvariantCulture, out var tenant) || tenant == Uuid.Empty ||
            !Uuid.TryParse(fact.Owner.Resource, CultureInfo.InvariantCulture, out var request) || request == Uuid.Empty ||
            fact.Metadata is not { IsAudit: false } metadata || metadata.EventId == Uuid.Empty ||
            metadata.AggregateId != request || metadata.AggregateVersion != (ulong)fact.Revision ||
            fact.Revision < 2 || fact.Generation < 1 || fact.Intent.Schema != AuditorSampleSelectionIntent.Schema)
            throw Invalid("The selection locator is not an exact native request admission.");
        var intent = await content.ReadJsonAsync<AuditorSampleSelectionIntent>(tenant.ToString(), fact.Intent, ct)
            .ConfigureAwait(false);
        if (intent.ProgramId == Uuid.Empty || intent.RequestId != request || intent.SelectionId == Uuid.Empty ||
            intent.SelectionId != fact.Operation.LogicalId || intent.Header.RootOperationId != intent.SelectionId ||
            intent.RequestGeneration != (ulong)fact.Generation || intent.ParentAdmissionRevision != (ulong)fact.Revision ||
            intent.PopulationId != fact.PopulationId || intent.PopulationVersion != fact.PopulationVersion ||
            intent.SupersedesSelectionId != fact.SupersedesSelectionId || intent.RecordedAt != fact.Operation.AcceptedAt ||
            !HeaderMatches(intent.Header, fact.Operation) ||
            fact.Owner != new AuditorRequest(tenant, intent.ProgramId, request).Stream ||
            !NativeOwnerHistory.IsOwnRoot(new AuditorRequest(tenant, intent.ProgramId, request), fact.Operation,
                fact.SupersedesSelectionId is null ? nameof(RecordSampleSelection) : nameof(AmendSampleSelection),
                (ulong)fact.Revision - 1))
            throw Invalid("The selection locator differs from its exact frozen request admission.");
        var parent = await AuditorRequestDirectorySchema.Requests.GetAsync(Transaction, request, ct).ConfigureAwait(false);
        if (parent is null)
            throw new ImmutableContentUnavailableException("The selection's verified creation identity is not indexed yet.");
        if (parent.TenantId != tenant || parent.ProgramId != intent.ProgramId || parent.AuditorRequestId != request)
            throw Invalid("The selection locator differs from the verified immutable request creation scope.");
        var locator = new AuditorSampleSelectionLocator(tenant, intent.ProgramId, request, intent.SelectionId, intent.RecordedAt);
        var key = $"{intent.ProgramId}:{request}:{intent.SelectionId}";
        var existing = await AuditorRequestDirectorySchema.Selections.GetAsync(Transaction, key, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing != locator)
                throw Invalid("An admitted selection locator cannot change its original owner or time.");
            return;
        }
        await AuditorRequestDirectorySchema.Selections.InsertAsync(Transaction, locator, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<AuditorRequestSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var transaction = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AuditorRequestDirectorySchema.Requests.QueryAsync(transaction,
            AuditorRequestDirectorySchema.ByProgramCreated.Query().WithPrefix(programId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<AuditorSampleSelectionLocator>> FindSelectionAsync(Uuid tenantId, Uuid programId,
        Uuid selectionId, CancellationToken ct = default)
    {
        await using var transaction = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var matches = await AuditorRequestDirectorySchema.Selections.QueryAsync(transaction,
            AuditorRequestDirectorySchema.ByProgramSelection.Query().WithPrefix(programId.ToString(), selectionId.ToString())
                .Take(2), ct).ConfigureAwait(false);
        return matches.Items;
    }

    static bool HeaderMatches(DurableIntentHeader header, OperationProvenance operation) =>
        header.RootOperationId == operation.RootOperationId && header.FirstOwner == (operation.FirstOwner ?? operation.Owner) &&
        header.AffectedOwner == operation.AffectedOwner && header.ExpectedRevisionOwner == operation.ExpectedRevisionOwner &&
        header.OriginalActor == operation.Actor && header.Episode == operation.Episode &&
        header.OriginalExpectedRevision == operation.OriginalExpectedRevision &&
        header.AuthoredIntentSha256 == operation.AuthoredIntentSha256 && header.AcceptedAt == operation.AcceptedAt;

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
