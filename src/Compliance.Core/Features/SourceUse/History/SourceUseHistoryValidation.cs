using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.SourceUse.History;

/// <summary>Transient full-history validation; never retained by the returned owner or a singleton cache.</summary>
sealed class SourceUseHistoryValidation(EventStreamAddress owner)
{
    public Dictionary<SourceUseSlot, SourceUseEntry> Entries { get; } = [];
    public Dictionary<SourceUseSlot, SourceUseEntry> Active { get; } = [];
    public ulong Generation { get; private set; }
    public string Digest { get; private set; } = SourceUseHistoryDigest.Empty;

    public void ObserveOwnerFact(DomainEvent fact) => Digest = SourceUseHistoryDigest.Append(Digest, fact);

    public void Observe(SourceUsePurpose purpose, bool granted, ISourceUseFact fact)
    {
        var slot = SourceUseSlot.Of(owner, purpose, fact);
        if (granted)
        {
            if (Entries.ContainsKey(slot))
                throw new InvalidOperationException("A source use is granted at most once and never after its fence.");
            Generation = checked(Generation + 1);
            var entry = new SourceUseEntry(Generation, (DomainEvent)fact, null);
            Entries.Add(slot, entry);
            Active.Add(slot, entry);
            return;
        }
        if (Entries.TryGetValue(slot, out var previous) && previous.IsSettled)
            throw new InvalidOperationException("A source use is settled at most once.");
        Entries[slot] = previous is null
            ? new SourceUseEntry(0, null, (DomainEvent)fact)
            : previous with { SettleFact = (DomainEvent)fact };
        Active.Remove(slot);
    }

    public static bool Same(SourceUseEntry actual, SourceUseEntry expected) =>
        actual.ReservationGeneration == expected.ReservationGeneration &&
        SameFact(actual.GrantFact, expected.GrantFact) && SameFact(actual.SettleFact, expected.SettleFact);

    static bool SameFact(DomainEvent? actual, DomainEvent? expected) => actual is null ? expected is null
        : expected is not null && actual.GetType() == expected.GetType() && actual.Equals(expected) &&
            actual.Metadata == expected.Metadata;
}
