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

    public static async ValueTask<Result> RaiseAsync<TRequest>(NativeOperationExecutor operations,
        IAggregateReader reader, IImmutableContentStore content, IExecutionContext context, string commandKind,
        TRequest request, Uuid tenantId, Uuid programId, Uuid findingId, FindingSource source, string title,
        string affectedScope, Uuid ownerMemberId, IReadOnlyList<FindingLink> links, string? correctiveAction,
        ActorReference actor, DateTimeOffset now, CancellationToken ct) where TRequest : notnull
    {
        var snapshot = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId, findingId), ct)
            .ConfigureAwait(false);
        var provenance = FindingContent.Provenance(findingId, commandKind, snapshot.Stream, actor, null,
            FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FindingRaiseTerms? terms = null;
        ImmutableContentRef? linksReference = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        InitialActionAdmission? initial = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var dueOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(DefaultDueDays);
            if (correctiveAction is not null && !FindingRules.IsBoundedText(correctiveAction, FindingRules.MaximumTextLength))
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "An initial corrective action needs bounded description."));
            var prepared = snapshot.PrepareRaise();
            if (!prepared.IsSuccess)
                return Result.Failure(prepared.Error!);
            AddCorrectiveAction? authored = null;
            if (correctiveAction is not null)
                authored = new AddCorrectiveAction(FindingRules.RoutedActionId(findingId), tenantId, programId,
                    findingId, 1, correctiveAction.Trim(), ownerMemberId, dueOn);
            terms = new FindingRaiseTerms(source, title, source.SourceText, DefaultSeverity, affectedScope,
                ownerMemberId, dueOn, actor, now, "routed", authored is null ? null : ContentDigest.OfJson(authored));
            linksReference = await FindingContent.WriteAsync(content, tenantId, FindingContent.LinksSchema,
                FindingRules.Trimmed(links), ct).ConfigureAwait(false);
            if (authored is not null)
                initial = await InitialActionAsync(content, tenantId, snapshot.Stream, provenance, authored, now, ct)
                    .ConfigureAwait(false);
            var unit = await FindingContent.FreezeAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct)
                .ConfigureAwait(false);
            (response, result) = unit;
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(tenantId, programId, findingId, findingId), finding => finding.Acceptance,
            finding => FindingCommands.Prepared(finding.PrepareRaise(), response),
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
            root.Actor, stream, root.Episode, null, root.AuthoredIntentSha256, now, stream, stream);
        var header = FindingContent.Header(operation, now);
        var content1 = await FindingContent.WriteDeterministicAsync(content, tenantId, authored.ActionId,
            FindingContent.ActionAuthoredSchema, authored, ct).ConfigureAwait(false);
        var intent = await FindingContent.WriteDeterministicAsync(content, tenantId, authored.ActionId,
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
