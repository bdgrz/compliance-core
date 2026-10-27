using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications.Readers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Seals the complete frozen plan of a batch as the root native acceptance of its command and starts
///     <c>ApplicationImportExecutionProcess</c>. The plan freezes every row, canonical target group and source claim
///     as immutable content, with the exact source revision and the current retirement impact, before the batch is
///     touched; an exact retry replays the original 202 acknowledgment even after the batch or its targets changed.
/// </summary>
public sealed class AcceptApplicationImportHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, IDomainEventReader events, IApplicationChangeImpactReader? impactReader = null)
    : IRequestHandler<AcceptApplicationImport, OperationAcceptedResponse>
{
    const string CommandKind = "AcceptApplicationImport";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<AcceptApplicationImport> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact import source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(IRequestContext<AcceptApplicationImport> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Acceptance requires a personal Bdgrz user over HTTP.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.BatchId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A command id, tenant, batch and non-negative expected revision are required.");
        var (memberId, display) = ApplicationActor.From(context);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ApplicationImportBatch(request.TenantId, request.BatchId,
            request.CommandId), ct).ConfigureAwait(false);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind,
            ActorReference.ForMember(memberId, display), snapshot.Stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, snapshot.Stream,
            snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // An acceptance that was already sealed resolves from the owner's facts before any guard is evaluated.
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? plan = null;
        long rowCount = 0, groupCount = 0;
        ApplicationImportBatchPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ApplicationImportBatch>(binding,
            () => new ApplicationImportBatch(request.TenantId, request.BatchId, request.CommandId),
            batch => batch.Acceptance,
            async (batch, prepareCt) =>
            {
                var prepared = batch.PrepareAcceptance(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var frozen = await FreezeAsync(request, batch, provenance, memberId, display, userId, now, prepareCt)
                    .ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                (plan, rowCount, groupCount) = frozen.Value;
                response = await ApplicationImportContent.AcceptedResponseAsync(content, request.TenantId,
                    request.CommandId, now, prepareCt).ConfigureAwait(false);

                preparedCut = ApplicationImportBatchPreparedCut.Capture(batch);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (ApplicationImportBatch batch, ImmutableContentRef acknowledgment) =>
            {
                if (preparedCut?.Matches(batch) != true)
                    return ApplicationImportBatchPreparedCut.Changed();
                return batch.SealPlan(provenance,
                plan!.Sha256, plan, rowCount, groupCount, acknowledgment);
            }), context, ct).ConfigureAwait(false);
        return await ApplicationImportContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Plan, long Rows, long Groups)>> FreezeAsync(
        AcceptApplicationImport request, ApplicationImportBatch batch, OperationProvenance provenance, Uuid memberId,
        string display, Uuid userId, DateTimeOffset now, CancellationToken ct)
    {
        if (!batch.IsCreated || batch.Staged is not { } staged)
            return Fail(RequestErrorKind.NotFound, "The import batch was not found.");
        var prepared = batch.PrepareAcceptance(request.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, long, long)>.Failure(prepared.Error!);
        var history = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
        IReadOnlyList<ApplicationImportStagedRow> rows;
        try
        {
            rows = await ApplicationImportBatchRows.ReadAsync(content, batch, history, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Fail(RequestErrorKind.Conflict, "The staged rows cannot be verified.", error is ImmutableContentUnavailableException);
        }

        var loadedSource = await ApplicationImportSourceView.LoadAsync(reader, content, events, request.TenantId,
            batch.SourceKey!, batch.SourceNamespace!, ct: ct).ConfigureAwait(false);
        if (!loadedSource.IsSuccess)
            return Result<(ImmutableContentRef, long, long)>.Failure(loadedSource.Error!);
        var source = loadedSource.Value!;
        if (source.Source.ActiveBatchId is { } active && active != batch.Id)
            return Fail(RequestErrorKind.Conflict, "Another batch is accepting for this source.", true);

        ApplicationImportOmissionProposalContent? proposal = null;
        string? proposalSha = null;
        if (batch.HasCurrentProposal)
        {
            try
            {
                proposal = await ApplicationImportContent.ReadAsync<ApplicationImportOmissionProposalContent>(content,
                    request.TenantId, batch.Proposal!.Proposal, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Fail(RequestErrorKind.Conflict, "The sealed omission proposal cannot be verified.", error is ImmutableContentUnavailableException);
            }

            // A proposal is bound to the exact source snapshot it was prepared against.
            if (proposal.SourcePosition != source.Source.CommittedStreamPosition ||
                proposal.ContentSha256 != batch.ContentDigest)
                proposal = null;
            else
                proposalSha = batch.Proposal.ProposalSha256;
        }

        var missing = source.Missing(batch, rows);
        var impacts = new Dictionary<Uuid, ApplicationChangePreview>();
        if (missing.Count > 0 && proposal is not null)
        {
            if (impactReader is null)
                return Fail(RequestErrorKind.Conflict,
                    "Missing source claims require a current sealed retirement proposal and complete impact evidence.");
            foreach (var retirement in proposal.Rows)
            {
                var impact = await impactReader.ReadAsync(new PreviewApplicationChange(request.TenantId,
                    retirement.ApplicationId, retirement.ExpectedApplicationRevision, "retire"), userId, ct)
                    .ConfigureAwait(false);
                if (!impact.IsSuccess)
                    return Result<(ImmutableContentRef, long, long)>.Failure(impact.Error!);
                if (!ApplicationImportRetirementImpact.IsUsable(impact.Value!, request.TenantId, retirement))
                    return Fail(RequestErrorKind.Conflict,
                        "Retirement is blocked because complete current impact and reliance evidence is unavailable.");
                impacts.Add(retirement.ApplicationId, impact.Value!);
            }
        }

        var planned = ApplicationImportPlanner.PlanRows(batch, history, rows, source, proposal, proposalSha, impacts);
        if (!planned.IsSuccess)
            return Result<(ImmutableContentRef, long, long)>.Failure(planned.Error!);
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        foreach (var row in planned.Value!)
            if (!targets.ContainsKey(row.ApplicationId))
                targets.Add(row.ApplicationId, await reader.HydrateApplicationAsync(request.TenantId,
                    row.ApplicationId, ct).ConfigureAwait(false));
        var groups = ApplicationImportPlanner.PlanGroups(request.TenantId, planned.Value, targets);
        if (!groups.IsSuccess)
            return Result<(ImmutableContentRef, long, long)>.Failure(groups.Error!);

        var header = new DurableIntentHeader(request.CommandId, batch.Stream, batch.Stream, batch.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256,
            now);
        var cut = new AuthoritativeReadCut($"stream://{source.Source.Stream.Realm}/{source.Source.Stream.Area}/{source.Source.Stream.Resource}",
            source.Source.CommittedStreamPosition, now);
        var (planRef, _, frozenGroups) = await ApplicationImportPlanWriter.FreezeAsync(content, request.TenantId,
            batch.Id, header, batch, source, planned.Value, groups.Value!, memberId, display, cut, ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, long, long)>.Success((planRef, planned.Value.Count, frozenGroups.Count));
    }

    static Result<(ImmutableContentRef, long, long)> Fail(RequestErrorKind kind, string message,
        bool transient = false) =>
        Result<(ImmutableContentRef, long, long)>.Failure(new RequestError(kind, message, transient));

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
