using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Operations.Readers;

public sealed class TypeIICalendarReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<TypeIICalendarReadSnapshot> ReadAsync(Uuid tenant, Uuid program,
        IReadOnlyList<Uuid>? selectedRoots = null, Uuid selectedLogicalId = default, CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new TypeIIObservationCalendar(tenant, program, selectedLogicalId), ct).ConfigureAwait(false),
            selectedRoots, ct).ConfigureAwait(false);
    public async ValueTask<TypeIICalendarReadSnapshot> ReadAsync(TypeIIObservationCalendar owner,
        IReadOnlyList<Uuid>? selectedRoots = null, CancellationToken ct = default)
    {
        var native = await TypeIICalendarNativeHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        if (owner.Header is null)
            return new(owner, TypeIICalendarContent.Empty(owner.TenantId, owner.Id), new(owner.TenantId, owner.Id), [], new Dictionary<long, DomainEvent>());
        if (owner.CurrentAuthority is not { Schema: TypeIICalendarContent.AuthoritySchema } reference)
            throw Invalid("The calendar has no exact immutable current authority.");
        var authority = await content.ReadJsonAsync<TypeIICalendarAuthority>(owner.TenantId.ToString(), reference, ct).ConfigureAwait(false);
        TypeIICalendarContent.Validate(authority);
        if (TypeIICalendarContent.Header(authority) != owner.Header)
            throw Invalid("The calendar immutable current authority differs from its hydrated native cut.");
        var selected = authority.Live.ToDictionary(value => value.OperationId);
        var roots = (selectedRoots ?? []).Where(id => id != Uuid.Empty).Distinct().ToArray();
        var unresolved = roots.Where(id => !selected.ContainsKey(id)).ToHashSet();
        var history = new List<TypeIICalendarHistoryRead>();
        var head = authority.ReleasedHistory;
        var seen = new HashSet<Uuid>();
        while (unresolved.Count != 0 && head is not null)
        {
            if (head.Schema != TypeIICalendarContent.HistorySchema || !seen.Add(head.ContentId))
                throw Invalid("The calendar terminal-history reference repeats or has another schema.");
            var node = await content.ReadJsonAsync<TypeIICalendarHistoryNode>(owner.TenantId.ToString(), head, ct).ConfigureAwait(false);
            if (node is null || node.Closed is null)
                throw Invalid("The calendar terminal-history node is malformed.");
            history.Add(new(head, node));
            foreach (var root in unresolved.ToArray())
                if (node.Closed.FirstOrDefault(value => value is not null && value.OperationId == root) is { } closed)
                {
                    selected.Add(root, closed);
                    unresolved.Remove(root);
                }
            head = node.Previous;
        }
        // Validate the complete prefix for every requested root. A missing link never proves absence.
        foreach (var root in roots)
            TypeIICalendarContent.Select(authority, history, root);
        if (ContentDigest.OfJson(native.Reservations.Where(value => value.IsLive).OrderBy(value => value.Generation).ToArray()) !=
            ContentDigest.OfJson(authority.Live.OrderBy(value => value.Generation).ToArray()) ||
            selected.Values.Any(value => native.Find(value.OperationId) != value))
            throw Invalid("The calendar immutable live or selected terminal body differs from its exact native transition history.");
        var rules = new TypeIICalendarRules(owner.TenantId, owner.Id, authority.Revision, authority.ReservationGeneration, selected.Values);
        var facts = await FactsAsync(owner, selected.Values, ct).ConfigureAwait(false);
        return new(owner, authority, rules, history.AsReadOnly(), facts);
    }
    public async ValueTask<TypeIICalendarReadSnapshot> ReadForAuthorizationAsync(Uuid tenant, Uuid program, Uuid eventId, CancellationToken ct = default)
    {
        var stream = new TypeIIObservationCalendar(tenant, program).Stream;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(stream.Realm, stream.Area, stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (TypeIICalendarContent.Metadata(record.Event).IsAudit || TypeIICalendarContent.Metadata(record.Event).EventId != eventId)
                continue;
            if (record.Stream != stream || record.Event is not TypeIIPeriodRangeAuthorized fact || fact.Header is not { } header || fact.Operation is not { } operation || fact.Owner != stream || operation.Owner != stream ||
                header.TenantId != tenant || header.ProgramId != program || fact.Metadata.AggregateId != program)
                throw Invalid("The selected authorization is another calendar's native fact.");
            return await ReadAsync(tenant, program, [fact.Operation.RootOperationId], ct: ct).ConfigureAwait(false);
        }
        return await ReadAsync(tenant, program, ct: ct).ConfigureAwait(false);
    }
    async ValueTask<IReadOnlyDictionary<long, DomainEvent>> FactsAsync(TypeIIObservationCalendar owner,
        IEnumerable<TypeIICalendarReservationContent> selected, CancellationToken ct)
    {
        var states = selected.ToArray();
        var needed = states.SelectMany(value => new[] { value.AuthorizationRevision, value.SettlementRevision }).Where(value => value is not null).Select(value => value!.Value).ToHashSet();
        var facts = new Dictionary<long, DomainEvent>();
        var ids = new HashSet<Uuid>();
        var sequence = 0L;
        var position = 0UL;
        DomainEvent? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.Stream.Realm, owner.Stream.Area, owner.Stream.Resource), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (TypeIICalendarContent.Metadata(record.Event).IsAudit)
                continue;
            var fact = record.Event;
            var header = TypeIICalendarContent.FactHeader(fact);
            var metadata = TypeIICalendarContent.Metadata(fact);
            if (record.Stream != owner.Stream || header is null || header.TenantId != owner.TenantId || header.ProgramId != owner.Id ||
                header.Revision != sequence + 1 || metadata.AggregateId != owner.Id || metadata.AggregateVersion <= position ||
                metadata.EventId == Uuid.Empty || !ids.Add(metadata.EventId) || fact is not INativeOperationFact { Operation: { } operation } || operation.Owner != owner.Stream)
                throw Invalid("The calendar selected native history has another exact owner, event identity or sequence.");
            if (header.Revision > owner.Revision)
                break;
            if (needed.Contains(header.Revision))
                facts.Add(header.Revision, fact);
            sequence = header.Revision;
            position = metadata.AggregateVersion;
            last = fact;
            if (sequence == owner.Revision)
                break;
        }
        if (sequence != owner.Revision || last is null || needed.Any(revision => !facts.ContainsKey(revision)))
            throw new ImmutableContentUnavailableException("The calendar exact native source cut is not visible yet.");
        if (TypeIICalendarContent.FactHeader(last) != owner.Header || TypeIICalendarContent.FactAuthority(last) != owner.CurrentAuthority)
            throw Invalid("The calendar source history differs from its hydrated native authority.");
        foreach (var candidate in facts.Values)
        {
            var operation = ((INativeOperationFact)candidate).Operation;
            var selectedOwner = await reader.HydrateAsync(new TypeIIObservationCalendar(owner.TenantId, owner.Id, operation.LogicalId), ct).ConfigureAwait(false);
            if (selectedOwner.Acceptance.SelectedFact is not { } actual)
                throw new ImmutableContentUnavailableException("The selected actual calendar native acceptance is not visible yet.");
            if (actual.GetType() != candidate.GetType() || !actual.Equals(candidate) || TypeIICalendarContent.Metadata(actual) != TypeIICalendarContent.Metadata(candidate))
                throw Invalid("The selected calendar proof differs from its actual native acceptance and metadata.");
        }
        foreach (var state in states)
        {
            if (state.AuthorizationRevision is { } authorized && (facts[authorized] is not TypeIIPeriodRangeAuthorized grant ||
                TypeIICalendarContent.Binding(TypeIICalendarContent.Change(grant)) != state.AuthorizationBindingSha256 ||
                grant.Header.ReservationGeneration != state.Generation || grant.Revision != state.AuthorizationRevision ||
                grant.Operation.RootOperationId != state.OperationId || grant.PeriodId != state.PeriodId || grant.Kind != state.Kind || grant.NewRange != state.Range || grant.PriorOperationId != state.PriorOperationId))
                throw Invalid("The selected calendar reservation differs from its actual authorization source.");
            if (state.SettlementRevision is { } settled && (facts[settled] is not TypeIIPeriodRangeSettled end || TypeIICalendarContent.Binding(TypeIICalendarContent.Change(end)) != state.SettlementBindingSha256 || end.Revision != state.SettlementRevision || end.PeriodId != state.PeriodId ||
                (state.State == TypeIIRangeReservationState.Fenced ? end.Settlement != TypeIIRangeSettlementKind.Fenced || end.TargetOperationId != state.OperationId :
                    state.State == TypeIIRangeReservationState.Active ? end.Settlement != TypeIIRangeSettlementKind.Activated || end.TargetOperationId != state.OperationId :
                    !(end.Settlement == TypeIIRangeSettlementKind.Released && end.TargetOperationId == state.OperationId ||
                        end.Settlement == TypeIIRangeSettlementKind.Activated && end.ReleasedOperationId == state.OperationId))))
                throw Invalid("The selected terminal/live calendar state differs from its actual settlement source.");
        }
        return facts;
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
