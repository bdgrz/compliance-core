using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Complete transient native/legacy history, with exact raw prefix before any selected receipt escapes.</summary>
public static class PersonHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(Person owner, IDomainEventReader events,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        PersonHeader? header = null;
        Bdgrz.Compliance.Features.Proofs.ImmutableContentRef? current = null;
        var logical = new HashSet<Uuid>();
        var digest = NativeReplayCommitment.Empty;
        var metadata = NativeReplayCommitment.Empty;
        var known = NativeReplayCommitment.Empty;
        foreach (var fact in facts)
        {
            if (fact is IPersonNativeFact native)
            {
                header = NextNative(owner.TenantId, owner.Id, owner.Stream, header, fact);
                current = native.Content;
                if (!logical.Add(native.Operation.LogicalId)) throw Invalid("A person history repeats an unselected native operation.");
                if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                    (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                    throw Invalid("The selected person receipt differs from its actual payload and full metadata.");
            }
            else
            {
                if (current is not null) throw Invalid("Read-only legacy person history cannot follow native content authority.");
                header = NextLegacy(owner.TenantId, owner.Id, header, fact);
            }
            digest = NativeReplayCommitment.Append(digest, fact);
            metadata = NativeReplayCommitment.Append(metadata, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                known = NativeReplayCommitment.Append(known, fact, true);
        }
        if (header != owner.Header || current != owner.CurrentContent)
            throw Invalid("The person authority differs from its exact complete native prefix.");
        owner.ReplayCut.Confirm(owner, digest, metadata, known);
        return facts;
    }
    internal static PersonHeader NextLegacy(Uuid tenant, Uuid person, PersonHeader? before, DomainEvent fact)
    {
        switch (fact)
        {
            case PersonRecorded value when before is null && value.TenantId == tenant && value.PersonId == person:
                RequireValues(tenant, person, value.DisplayName, value.WorkEmail, value.PersonalContact, value.Actor, value.ChangedAt);
                return new(tenant, person, 1, null, PersonContent.ValuesSha256(value.DisplayName, value.WorkEmail, value.PersonalContact), value.Actor, value.ChangedAt);
            case PersonRevised value when before is not null && value.TenantId == tenant && value.PersonId == person && value.Revision == before.Revision + 1:
                RequireValues(tenant, person, value.DisplayName, value.WorkEmail, value.PersonalContact, value.Actor, value.ChangedAt);
                return before with { Revision = value.Revision, ValuesSha256 = PersonContent.ValuesSha256(value.DisplayName, value.WorkEmail, value.PersonalContact), LastChangedBy = value.Actor, LastChangedAt = value.ChangedAt };
            case PersonMembershipCorrelated value when before is not null && value.TenantId == tenant && value.PersonId == person &&
                value.Revision == before.Revision + 1 && value.UserId != Uuid.Empty && value.UserId != before.CorrelatedUserId && value.Actor is not null && value.ChangedAt != default:
                return before with { Revision = value.Revision, CorrelatedUserId = value.UserId, LastChangedBy = value.Actor, LastChangedAt = value.ChangedAt };
            default: throw Invalid("The read-only legacy person source has another identity, revision or values transition.");
        }
    }
    internal static PersonHeader NextNative(Uuid tenant, Uuid person, EventStreamAddress stream, PersonHeader? before, DomainEvent fact)
    {
        if (fact is not IPersonNativeFact native || native.Header is not { } header || header.TenantId != tenant || header.PersonId != person ||
            header.CorrelatedUserId == Uuid.Empty || header.ValuesSha256 is not { Length: 64 } || !header.ValuesSha256.All(Uri.IsHexDigit) || native.Operation is null || !ValidActor(header.LastChangedBy) || !ValidActor(native.Operation.Actor) ||
            native.Operation.FirstOwner != stream || native.FirstResponse is null || native.OperationResult is null ||
            !PersonContent.Reference(native.Content, PersonContent.ViewSchema) || !PersonContent.Reference(native.Change, PersonContent.ChangeSchema) ||
            !PersonContent.Reference(native.FirstResponse, FirstResponses.Schema) || !PersonContent.Reference(native.OperationResult, "mutation-result-content.v1"))
            throw Invalid("The native person fact has another bounded source identity or immutable content reference.");
        var command = fact switch
        {
            PersonRegistrationRecorded => nameof(RecordPerson),
            PersonDetailsRevised => nameof(RevisePerson),
            PersonMembershipCorrelationRecorded => nameof(CorrelatePersonMembership),
            _ => throw Invalid("Another native person transition kind."),
        };
        if (native.Operation.Owner != stream || !NativeOwnerHistory.IsOwnRoot(new Person(tenant, person), native.Operation, command,
                command == nameof(RecordPerson) ? null : (ulong?)before?.Revision) ||
            command == nameof(RecordPerson) && (before is not null || native.Operation.LogicalId != person || !native.Changed || header.Revision != 1 || header.CorrelatedUserId is not null) ||
            command != nameof(RecordPerson) && (before is null || header.Revision != before.Revision + (native.Changed ? 1 : 0)) ||
            command == nameof(RevisePerson) && (!native.Changed || header.CorrelatedUserId != before!.CorrelatedUserId) ||
            command == nameof(CorrelatePersonMembership) && native.Changed == (header.CorrelatedUserId == before!.CorrelatedUserId) ||
            native.Changed && (header.LastChangedBy != native.Operation.Actor || header.LastChangedAt != native.Operation.AcceptedAt) ||
            !native.Changed && header != before)
            throw Invalid("The native person fact differs from its exact original root, revision or correlation transition.");
        return header;
    }
    static bool ValidActor(ActorReference? actor) => actor is not null && !string.IsNullOrWhiteSpace(actor.Display) &&
        (actor.Kind == "system_process" ? !string.IsNullOrWhiteSpace(actor.Id) :
            actor.Kind is "member" or "platform_operator" or "firm_staff" &&
            Uuid.TryParse(actor.Id, System.Globalization.CultureInfo.InvariantCulture, out var id) && id != Uuid.Empty);
    static void RequireValues(Uuid tenant, Uuid person, string name, string? email, PersonalContactDetails? contact,
        ActorReference actor, DateTimeOffset at)
    {
        var rules = new PersonRules(tenant, person);
        if (!ValidActor(actor) || at == default || !rules.Record(name, email, contact, actor, at).IsSuccess ||
            rules.Current!.DisplayName != name || rules.Current.WorkEmail != email || rules.Current.PersonalContact != contact)
            throw Invalid("The legacy person values are not the original normalized source values.");
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
