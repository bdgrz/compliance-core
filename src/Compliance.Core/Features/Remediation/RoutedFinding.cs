using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Raises a reactor-routed finding with the product defaults (medium severity, due in 30 days) and an optional
///     default corrective action. The finding identity is the logical operation, so replays are exact. The action, when
///     any, is admitted atomically with the raise under its deterministic id and materialized independently.
/// </summary>
static class RoutedFinding
{
    public const int DefaultDueDays = 30;
    public const string DefaultSeverity = "medium";

    public static ValueTask<Result> RaiseAsync<TRequest>(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, string commandKind,
        TRequest request, Uuid tenantId, Uuid programId, Uuid findingId, FindingSource source, string title,
        string affectedScope, Uuid ownerMemberId, IReadOnlyList<FindingLink> links, string? correctiveAction,
        ActorReference actor, DateTimeOffset now, CancellationToken ct) where TRequest : notnull =>
        RaiseCoreAsync(operations, reader, content, context, commandKind, request, tenantId, programId, findingId,
            source, title, affectedScope, ownerMemberId, links, correctiveAction, actor, now, null, ct);

    /// <summary>Resolves mutable routed source terms only after exact native acceptance has been checked.</summary>
    public static ValueTask<Result> RaiseFromSourceAsync<TRequest>(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, string commandKind,
        TRequest request, Uuid tenantId, Uuid programId, Uuid findingId, ActorReference actor, DateTimeOffset now,
        Func<CancellationToken, ValueTask<Result<RoutedFindingSource>>> prepareSource, CancellationToken ct)
        where TRequest : notnull =>
        RaiseCoreAsync(operations, reader, content, context, commandKind, request, tenantId, programId, findingId,
            null!, string.Empty, string.Empty, Uuid.Empty, [], null, actor, now, prepareSource, ct);

    static async ValueTask<Result> RaiseCoreAsync<TRequest>(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, string commandKind,
        TRequest request, Uuid tenantId, Uuid programId, Uuid findingId, FindingSource source, string title,
        string affectedScope, Uuid ownerMemberId, IReadOnlyList<FindingLink> links, string? correctiveAction,
        ActorReference actor, DateTimeOffset now, Func<CancellationToken, ValueTask<Result<RoutedFindingSource>>>? prepareSource,
        CancellationToken ct) where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(reader);
        var snapshot = new FindingAggregate(tenantId, programId, findingId, findingId);
        var provenance = FindingContent.Provenance(findingId, commandKind, snapshot.Stream, actor, null,
            FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        FindingRaiseTerms? terms = null;
        ImmutableContentRef? linksReference = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        InitialActionAdmission? initial = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            if (prepareSource is not null)
            {
                var resolved = await prepareSource(token).ConfigureAwait(false);
                if (!resolved.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(resolved.Error!);
                var selected = resolved.Value!;
                (source, title, affectedScope, ownerMemberId, links, correctiveAction) = (selected.Source,
                    selected.Title, selected.AffectedScope, selected.OwnerMemberId, selected.Links, selected.CorrectiveAction);
            }
            var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(DefaultDueDays);
            if (correctiveAction is not null && !FindingRules.IsBoundedText(correctiveAction, FindingRules.MaximumTextLength))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "An initial corrective action needs bounded description."));
            var prepared = owner.PrepareRaise();
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            AddCorrectiveAction? authored = null;
            if (correctiveAction is not null)
                authored = new AddCorrectiveAction(FindingRules.RoutedActionId(findingId), tenantId, programId,
                    findingId, 1, correctiveAction.Trim(), ownerMemberId, dueOn);
            terms = new FindingRaiseTerms(source, title, source.SourceText, DefaultSeverity, affectedScope,
                ownerMemberId, dueOn, actor, now, "routed", authored is null ? null : ContentDigest.OfJson(authored));
            linksReference = await FindingContent.WriteAsync(content, tenantId, FindingContent.LinksSchema,
                FindingRules.Trimmed(links), token).ConfigureAwait(false);
            if (authored is not null)
                initial = await InitialActionAsync(content, tenantId, owner.Stream, provenance, authored, now, token)
                    .ConfigureAwait(false);
            var unit = await FindingContent.FreezeAsync(content, tenantId, UnitResult.Schema, new UnitResult(), token)
                .ConfigureAwait(false);
            (response, result) = unit;
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(tenantId, programId, findingId, findingId), finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef frozen) =>
                finding.Raise(provenance, terms!, linksReference!, frozen, result!, initial)), context, ct)
            .ConfigureAwait(false);
        return outcome.IsSuccess ? Result.Success : Result.Failure(outcome.Error!);
    }

    static async ValueTask<InitialActionAdmission> InitialActionAsync(IImmutableContentStore content, Uuid tenantId,
        EventStreamAddress stream, OperationProvenance root, AddCorrectiveAction authored, DateTimeOffset now,
        CancellationToken ct)
    {
        var operation = new OperationProvenance(authored.ActionId, root.RootOperationId, null, "AdmitCorrectiveAction",
            root.Actor, stream, root.Episode, null, root.AuthoredIntentSha256, now, stream, stream, stream);
        var header = FindingContent.Header(operation, now);
        var content1 = await FindingContent.WriteAsync(content, tenantId,
            FindingContent.ActionAuthoredSchema, authored, ct).ConfigureAwait(false);
        var intent = await FindingContent.WriteAsync(content, tenantId,
            ChildAdmissionIntent.Schema, new ChildAdmissionIntent(header, authored.FindingId, 1,
                AdmittedChildKind.CorrectiveAction, authored.ActionId, content1,
                await EmptyProofsAsync(content, tenantId, authored.ActionId, ct).ConfigureAwait(false), 2), ct)
            .ConfigureAwait(false);
        return new InitialActionAdmission(operation, intent);
    }

    /// <summary>The empty selected source proof manifest of an admission that selected no external source.</summary>
    public static Task<ImmutableSelectionManifest> EmptyProofsAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid identity, CancellationToken ct) =>
        ImmutableSelectionManifests.WriteAsync<NativeFactReference>(content, tenantId.ToString(),
            Uuid.CreateVersion5(identity, "selected-source-proofs"), "source-fact-reference.v1", [],
            SelectionOrder.CanonicalKey, ct);
}
