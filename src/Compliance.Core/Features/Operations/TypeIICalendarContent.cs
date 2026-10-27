using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public static class TypeIICalendarContent
{
    public const string AuthoritySchema = "type-ii-calendar-live-authority.v1";
    public const string HistorySchema = "type-ii-calendar-terminal-history.v1";
    public static string Binding(object change) => ContentDigest.Sha256(change, change.GetType());
    public static TypeIICalendarAuthority Empty(Uuid tenant, Uuid program) => new(tenant, program, 0, 0, 0, null, []);
    public static TypeIIObservationCalendarHeader Header(TypeIICalendarAuthority value) => new(value.TenantId, value.ProgramId,
        value.Revision, value.ReservationGeneration, value.Live.Count, value.TerminalCount);
    public static void Validate(TypeIICalendarAuthority value)
    {
        if (value is null || value.TenantId == Uuid.Empty || value.ProgramId == Uuid.Empty || value.Revision < 0 || value.ReservationGeneration < 0 ||
            value.TerminalCount < 0 || value.Live is null || value.TerminalCount == 0 != (value.ReleasedHistory is null) ||
            (value.ReleasedHistory is not null && !Reference(value.ReleasedHistory, HistorySchema)) || value.Live.Any(item => !ValidReservation(item, value.Revision) || !item.IsLive) ||
            value.Live.Select(item => item.OperationId).Distinct().Count() != value.Live.Count ||
            value.Live.Select(item => item.Generation).Distinct().Count() != value.Live.Count || value.Live.Any(item => item.Generation > value.ReservationGeneration))
            throw Invalid("The calendar current authority has another exact scope, range or history cut.");
        for (var left = 0; left < value.Live.Count; left++)
            for (var right = left + 1; right < value.Live.Count; right++)
                if (value.Live[left].PeriodId != value.Live[right].PeriodId && value.Live[left].Range!.Overlaps(value.Live[right].Range!))
                    throw Invalid("The calendar immutable authority overlaps another period's live range.");
    }
    public static TypeIICalendarReservationContent? Select(TypeIICalendarAuthority authority,
        IReadOnlyList<TypeIICalendarHistoryRead> history, Uuid root)
    {
        if (authority.Live.FirstOrDefault(item => item.OperationId == root) is { } live)
            return live;
        var reference = authority.ReleasedHistory;
        var remaining = authority.TerminalCount;
        var revision = authority.Revision + 1;
        foreach (var step in history)
        {
            var node = step.Node;
            if (node is null || reference is null || step.Reference != reference || reference.Schema != HistorySchema ||
                !Matches(reference, node, HistorySchema) || node.TenantId != authority.TenantId || node.ProgramId != authority.ProgramId ||
                node.Revision < 1 || node.Revision >= revision || node.TerminalCount != remaining || node.Closed is not { Count: 1 } ||
                node.Closed.Any(item => !ValidReservation(item, node.Revision) || item.IsLive || item.SettlementRevision != node.Revision))
                throw Invalid("The calendar terminal-history proof differs from its exact immutable authority.");
            remaining -= node.Closed.Count;
            revision = node.Revision;
            reference = node.Previous;
            if (remaining < 0 || remaining == 0 != (reference is null))
                throw Invalid("The calendar terminal-history chain has another exact count.");
            if (node.Closed.FirstOrDefault(item => item.OperationId == root) is { } found)
                return found;
        }
        if (reference is not null || remaining != 0)
            throw Invalid("The selected calendar terminal membership or absence has not been proven.");
        return null;
    }
    public static DomainEventMetadata Metadata(DomainEvent value)
    {
        try
        { return value.Metadata; }
        catch (InvalidOperationException) { throw Invalid("The calendar source carries no committed event metadata."); }
    }
    public static IReadOnlyList<Uuid> RequiredRoots(object change) => change switch
    {
        TypeIICalendarAuthorizationChange { Operation: not null } value => new[] { value.Operation.RootOperationId, value.PriorOperationId ?? Uuid.Empty }.Where(id => id != Uuid.Empty).Distinct().ToArray(),
        TypeIICalendarSettlementChange value => new[] { value.TargetOperationId, value.ReleasedOperationId ?? Uuid.Empty }.Where(id => id != Uuid.Empty).Distinct().ToArray(),
        _ => throw Invalid("Another calendar mutation kind."),
    };
    public static async ValueTask<TypeIICalendarPrepared> FreezeAsync(IImmutableContentStore content,
        TypeIICalendarReadSnapshot before, TypeIICalendarRules after, CancellationToken ct = default)
    {
        var change = after.PendingChanges.Single();
        var closed = after.Reservations.Where(item => !item.IsLive && item.SettlementRevision == after.Revision).OrderBy(item => item.OperationId).ToArray();
        TypeIICalendarHistoryNode? node = null;
        var head = before.Authority.ReleasedHistory;
        if (closed.Length != 0)
        {
            node = new(after.TenantId, after.Id, after.Revision, before.Authority.TerminalCount + closed.Length, head, closed);
            head = await NativeContent.WriteAsync(content, after.TenantId, HistorySchema, node, ct).ConfigureAwait(false);
        }
        var authority = new TypeIICalendarAuthority(after.TenantId, after.Id, after.Revision, after.ReservationGeneration,
            before.Authority.TerminalCount + closed.Length, head, after.Reservations.Where(item => item.IsLive).OrderBy(item => item.Generation).ToArray());
        var reference = await NativeContent.WriteAsync(content, after.TenantId, AuthoritySchema, authority, ct).ConfigureAwait(false);
        var result = new TypeIICalendarPrepared(before.Authority, authority, reference, before.History, node, change);
        if (!Verify(result))
            throw Invalid("The calendar preparation does not prove its exact before/after decision.");
        return result;
    }
    public static bool Verify(TypeIICalendarPrepared prepared)
    {
        Validate(prepared.Before);
        Validate(prepared.After);
        if (!Matches(prepared.Authority, prepared.After, AuthoritySchema) ||
            prepared.Before.TenantId != prepared.After.TenantId || prepared.Before.ProgramId != prepared.After.ProgramId || prepared.After.Revision != prepared.Before.Revision + 1)
            return false;
        var selected = prepared.Before.Live.ToDictionary(item => item.OperationId);
        foreach (var root in RequiredRoots(prepared.Change))
            if (Select(prepared.Before, prepared.History, root) is { } terminal)
                selected.TryAdd(root, terminal);
        var rules = new TypeIICalendarRules(prepared.Before.TenantId, prepared.Before.ProgramId, prepared.Before.Revision,
            prepared.Before.ReservationGeneration, selected.Values);
        var result = prepared.Change switch
        {
            TypeIICalendarAuthorizationChange { Operation: not null, Intent: not null, NewRange: not null } value => rules.Authorize(value.Operation, value.Intent, value.PeriodId, value.Kind, value.NewRange, value.PriorOperationId),
            TypeIICalendarSettlementChange { Operation: not null, Intent: not null } value => rules.Settle(value.Operation, value.Intent, value.PeriodId, value.Settlement, value.TargetOperationId, value.ReleasedOperationId),
            _ => Result.Failure(new RequestError(RequestErrorKind.Conflict, "Another calendar mutation kind.")),
        };
        if (!result.IsSuccess || rules.Revision != prepared.After.Revision || rules.ReservationGeneration != prepared.After.ReservationGeneration ||
            ContentDigest.OfJson(rules.Reservations.Where(item => item.IsLive).OrderBy(item => item.Generation).ToArray()) != ContentDigest.OfJson(prepared.After.Live.ToArray()))
            return false;
        var closed = rules.Reservations.Where(item => !item.IsLive && item.SettlementRevision == rules.Revision).OrderBy(item => item.OperationId).ToArray();
        if (prepared.After.TerminalCount != prepared.Before.TerminalCount + closed.Length)
            return false;
        if (closed.Length == 0)
            return prepared.NewHistory is null && prepared.After.ReleasedHistory == prepared.Before.ReleasedHistory;
        var expected = new TypeIICalendarHistoryNode(rules.TenantId, rules.Id, rules.Revision, prepared.After.TerminalCount, prepared.Before.ReleasedHistory, closed);
        return prepared.NewHistory is not null && ContentDigest.OfJson(expected) == ContentDigest.OfJson(prepared.NewHistory) &&
            prepared.After.ReleasedHistory is { Schema: HistorySchema } head && Matches(head, expected, HistorySchema);
    }
    public static DomainEvent Fact(TypeIICalendarPrepared value) => value.Change switch
    {
        TypeIICalendarAuthorizationChange change => new TypeIIPeriodRangeAuthorized(change.Operation.Owner, value.After.Revision,
            TypeIIRangeEffects.Generation, change.Intent, null, change.Operation, null, null, change.PeriodId, change.Kind, change.NewRange,
            change.PriorOperationId, Header(value.After), value.Authority),
        TypeIICalendarSettlementChange change => new TypeIIPeriodRangeSettled(change.Operation.Owner, value.After.Revision,
            TypeIIRangeEffects.Generation, change.Intent, null, change.Operation, null, null, change.PeriodId, change.Settlement,
            change.TargetOperationId, change.ReleasedOperationId, Header(value.After), value.Authority),
        _ => throw Invalid("Another calendar mutation kind."),
    };
    public static object Change(DomainEvent value) => value switch
    {
        TypeIIPeriodRangeAuthorized fact => new TypeIICalendarAuthorizationChange(fact.Operation, fact.Intent, fact.PeriodId, fact.Kind, fact.NewRange, fact.PriorOperationId),
        TypeIIPeriodRangeSettled fact => new TypeIICalendarSettlementChange(fact.Operation, fact.Intent, fact.PeriodId, fact.Settlement, fact.TargetOperationId, fact.ReleasedOperationId),
        _ => throw Invalid("Another calendar native fact kind."),
    };
    public static TypeIIObservationCalendarHeader FactHeader(DomainEvent value) => value switch
    {
        TypeIIPeriodRangeAuthorized fact => fact.Header,
        TypeIIPeriodRangeSettled fact => fact.Header,
        _ => throw Invalid("Another calendar native fact kind."),
    };
    public static ImmutableContentRef FactAuthority(DomainEvent value) => value switch
    {
        TypeIIPeriodRangeAuthorized fact => fact.CurrentAuthority,
        TypeIIPeriodRangeSettled fact => fact.CurrentAuthority,
        _ => throw Invalid("Another calendar native fact kind."),
    };
    static bool Matches<T>(ImmutableContentRef? reference, T value, string schema) where T : notnull
    {
        if (!Reference(reference, schema))
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), ComplianceCoreJsonContext.Default);
        return reference!.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
    public static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty && value.Bytes >= 0 &&
        value.Schema == schema && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && value.Sha256 is { Length: 64 } && value.Sha256.All(Uri.IsHexDigit);
    static bool ValidReservation(TypeIICalendarReservationContent? value, long revision) => value is not null && value.OperationId != Uuid.Empty &&
        value.PeriodId != Uuid.Empty && (value.State == TypeIIRangeReservationState.Fenced
            ? value.Range is null && value.Generation == 0 && value.AuthorizationRevision is null && value.AuthorizationBindingSha256 is null &&
                Digest(value.SettlementBindingSha256) && value.Kind == TypeIIRangeOperationKind.Opening && value.PriorOperationId is null && value.SettlementRevision is >= 1 && value.SettlementRevision <= revision
            : value.State is TypeIIRangeReservationState.Pending or TypeIIRangeReservationState.Active or TypeIIRangeReservationState.Released &&
                value.Range is { IsValid: true } && value.Generation > 0 && Digest(value.AuthorizationBindingSha256) && value.AuthorizationRevision is >= 1 && value.AuthorizationRevision <= revision &&
                value.Kind is TypeIIRangeOperationKind.Opening or TypeIIRangeOperationKind.Amendment &&
                (value.Kind == TypeIIRangeOperationKind.Opening) == (value.PriorOperationId is null) &&
                (value.State == TypeIIRangeReservationState.Pending ? value.SettlementRevision is null && value.SettlementBindingSha256 is null :
                    Digest(value.SettlementBindingSha256) && value.SettlementRevision > value.AuthorizationRevision && value.SettlementRevision <= revision));
    static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
