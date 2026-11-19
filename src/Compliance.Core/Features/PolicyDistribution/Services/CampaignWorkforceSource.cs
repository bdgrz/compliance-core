using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Services;

/// <summary>Transient authoritative workforce reconstruction for a source-bound audience reaction.</summary>
sealed class CampaignWorkforceSource(IDomainEventReader events, PersonReader people)
{
    internal sealed record Observation(Uuid PersonId, string DisplayName, string SourceKind,
        ActorReference Actor, IReadOnlyList<FrozenWorkRelationship> Relationships, EventCursor Cut);

    public async Task<Observation> ReadAsync(Uuid tenantId, NativeFactReference source, CancellationToken ct)
    {
        if (source.Stream.Realm != tenantId.ToString() || source.Stream.Area != "work-relationships" ||
            source.EventId == Uuid.Empty || source.AggregateVersion == 0)
            throw new ImmutableContentIntegrityException("The workforce source names another tenant or area.");
        var records = new List<DomainEventRecord>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            "work-relationships"), EventCursor.Start, ct).ConfigureAwait(false))
            records.Add(record);
        var exact = records.SingleOrDefault(record => record.Stream == source.Stream &&
            record.Event.Metadata.EventId == source.EventId && record.Event.Metadata.AggregateVersion == source.AggregateVersion);
        if (exact is null)
            throw new ImmutableContentIntegrityException("The committed workforce source fact was not found.");
        var (triggerRelationship, kind, actor) = exact.Event switch
        {
            WorkRelationshipRecorded value when value.TenantId == tenantId => (value.RelationshipId, "recorded", value.Actor),
            WorkRelationshipStarted value when value.TenantId == tenantId => (value.RelationshipId, "started", value.Actor),
            WorkRelationshipDepartmentChanged value when value.TenantId == tenantId => (value.RelationshipId, "department", value.Actor),
            WorkRelationshipEnded value when value.TenantId == tenantId => (value.RelationshipId, "ended", value.Actor),
            WorkRelationshipRevised value when value.TenantId == tenantId => (value.RelationshipId, "revised", value.Actor),
            WorkRelationshipDatesCorrected value when value.TenantId == tenantId => (value.RelationshipId, "dates", value.Actor),
            _ => throw new ImmutableContentIntegrityException("The source is not a workforce audience change."),
        };
        if (source.Stream.Resource != triggerRelationship.ToString())
            throw new ImmutableContentIntegrityException("The workforce source payload names another relationship.");
        var roots = records.Select(record => record.Event).OfType<WorkRelationshipRecorded>().ToArray();
        var root = roots.SingleOrDefault(value => value.RelationshipId == triggerRelationship && value.TenantId == tenantId)
            ?? throw new ImmutableContentIntegrityException("The relationship has no authoritative recorded predecessor.");
        var relationships = new List<FrozenWorkRelationship>();
        foreach (var registered in roots.Where(value => value.PersonId == root.PersonId))
        {
            var stream = new EventStreamAddress(tenantId.ToString(), "work-relationships", registered.RelationshipId.ToString());
            var facts = records.Where(record => record.Stream == stream).ToArray();
            var terms = registered.Terms;
            var revision = 0L;
            foreach (var record in facts)
            {
                var scope = Scope(record.Event);
                if (scope.Tenant != tenantId || scope.Relationship != registered.RelationshipId ||
                    scope.Revision != revision + 1 || record.ResourceOffset != (ulong)revision ||
                    record.Event.Metadata.AggregateVersion != (ulong)revision + 1)
                    throw new ImmutableContentIntegrityException("The workforce relationship history has another scope or revision.");
                terms = record.Event switch
                {
                    WorkRelationshipRecorded value when revision == 0 => value.Terms,
                    WorkRelationshipRevised value => value.Terms,
                    WorkRelationshipStarted value => terms with { LifecycleStatus = "active", StartDate = value.StartDate },
                    WorkRelationshipLeaveStarted => terms with { LifecycleStatus = "on_leave" },
                    WorkRelationshipLeaveEnded => terms with { LifecycleStatus = "active" },
                    WorkRelationshipEnded value => terms with { LifecycleStatus = "ended", EndDate = value.EndDate },
                    WorkRelationshipDepartmentChanged value => terms with { Department = value.Department },
                    WorkRelationshipDatesCorrected value => terms with { StartDate = value.StartDate, EndDate = value.EndDate },
                    WorkRelationshipManagerChanged or WorkRelationshipSponsorChanged => terms,
                    _ => throw new ImmutableContentIntegrityException("The workforce relationship history contains an unknown fact."),
                };
                revision++;
            }
            relationships.Add(new FrozenWorkRelationship(registered.RelationshipId, revision, registered.PersonId,
                registered.SourceWorkerId, terms.WorkerType, terms.LifecycleStatus, terms.StartDate, terms.EndDate,
                terms.Department, null, null));
        }
        var person = await people.ReadAsync(tenantId, root.PersonId, ct).ConfigureAwait(false);
        if (person.View is null)
            throw new ImmutableContentIntegrityException("The workforce relationship's person has no verified source.");
        return new(root.PersonId, person.View.DisplayName, kind, actor, relationships,
            records.Count == 0 ? EventCursor.Start : records[^1].NextCursor);
    }

    public async Task<bool> IsCurrentAsync(Uuid tenantId, EventCursor cut, CancellationToken ct)
    {
        await using var pending = events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
            "work-relationships"), cut, ct).GetAsyncEnumerator(ct);
        return !await pending.MoveNextAsync().ConfigureAwait(false);
    }

    public static RosterAudience Audience(Observation observed, string kind, IReadOnlyList<string> teams, DateOnly today)
    {
        var present = observed.Relationships.Any(relationship => PolicyAudience.IsCurrent(relationship, today));
        var match = observed.Relationships.Where(relationship => PolicyAudience.Matches(kind, teams, relationship, today))
            .OrderByDescending(relationship => relationship.StartDate).FirstOrDefault();
        var matches = new Dictionary<Uuid, RosterMatch>();
        if (match is not null)
            matches.Add(observed.PersonId, new(observed.PersonId, observed.DisplayName, match.WorkerType,
                match.Department, match.StartDate));
        return new(matches, present ? new HashSet<Uuid> { observed.PersonId } : new HashSet<Uuid>());
    }

    static (Uuid Tenant, Uuid Relationship, long Revision) Scope(DomainEvent fact) => fact switch
    {
        WorkRelationshipRecorded value => (value.TenantId, value.RelationshipId, 1),
        WorkRelationshipRevised value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipStarted value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipLeaveStarted value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipLeaveEnded value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipEnded value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipDepartmentChanged value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipDatesCorrected value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipManagerChanged value => (value.TenantId, value.RelationshipId, value.Revision),
        WorkRelationshipSponsorChanged value => (value.TenantId, value.RelationshipId, value.Revision),
        _ => throw new ImmutableContentIntegrityException("The workforce relationship history contains an unknown fact."),
    };
}
