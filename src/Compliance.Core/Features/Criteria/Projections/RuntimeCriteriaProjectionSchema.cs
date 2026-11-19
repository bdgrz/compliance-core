using System.Globalization;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projections;

/// <summary>Keyed rows and an ordered index; no edition-sized record is stored.</summary>
static class RuntimeCriteriaProjectionSchema
{
    internal readonly record struct EditionKey(string Generation, Uuid EditionId);
    internal readonly record struct EntryKey(string Generation, Uuid EditionId, string Identifier);
    internal readonly record struct DisclosureKey(string Generation, Uuid EditionId, string Category, string Code);
    internal readonly record struct ValueOwnerKey(string Generation, Uuid OwnerId);
    internal readonly record struct ValueOutcomeKey(string Generation, Uuid OutcomeEventId);
    internal readonly record struct EditionFactKey(string Generation, Uuid EditionId, Uuid EventId);

    internal static readonly KvDirectoryIndex<RuntimeCriteriaEditionIndexRow> ByPublication = new(
        "by_publication", 1,
        static edition => [edition.Generation, edition.Status,
            edition.PublicationOrdinal.ToString("D20", CultureInfo.InvariantCulture), edition.EditionId.ToString()]);

    internal static readonly KvDirectoryIndex<RuntimeCriteriaEntryIndexRow> ByEditionIdentifier = new(
        "by_edition_identifier", 1,
        static entry => [entry.Generation, entry.EditionId.ToString(), entry.Identifier]);

    internal static readonly KvDirectory<RuntimeCriteriaEditionIndexRow, EditionKey> Editions = new(
        "runtime_criteria_editions", ComplianceCoreJsonContext.Default.RuntimeCriteriaEditionIndexRow,
        static edition => new EditionKey(edition.Generation, edition.EditionId),
        static key => [key.Generation, key.EditionId.ToString()], [ByPublication]);

    internal static readonly KvDirectory<RuntimeCriteriaEntryIndexRow, EntryKey> Entries = new(
        "runtime_criteria_entries", ComplianceCoreJsonContext.Default.RuntimeCriteriaEntryIndexRow,
        static entry => new EntryKey(entry.Generation, entry.EditionId, entry.Identifier),
        static key => [key.Generation, key.EditionId.ToString(), key.Identifier], [ByEditionIdentifier]);

    internal static readonly KvDirectory<RuntimeCriteriaProjectionClock, string> Clock = new(
        "runtime_criteria_projection_clock", ComplianceCoreJsonContext.Default.RuntimeCriteriaProjectionClock,
        static clock => clock.Generation, static generation => [generation], []);

    internal static readonly KvDirectory<RuntimeCriteriaEditionFactIndexRow, EditionFactKey> EditionFacts = new(
        "runtime_criteria_edition_facts", ComplianceCoreJsonContext.Default.RuntimeCriteriaEditionFactIndexRow,
        static fact => new EditionFactKey(fact.Generation, fact.EditionId, fact.EventId),
        static key => [key.Generation, key.EditionId.ToString(), key.EventId.ToString()], []);

    internal static readonly KvDirectory<RuntimeCriteriaValueClaimIndexRow, ValueOwnerKey> ValueClaims = new(
        "runtime_criteria_value_claims", ComplianceCoreJsonContext.Default.RuntimeCriteriaValueClaimIndexRow,
        static claim => new ValueOwnerKey(claim.Generation, claim.OwnerId),
        static key => [key.Generation, key.OwnerId.ToString()], []);

    internal static readonly KvDirectory<RuntimeCriteriaValuePendingIndexRow, ValueOwnerKey> PendingValues = new(
        "runtime_criteria_pending_values", ComplianceCoreJsonContext.Default.RuntimeCriteriaValuePendingIndexRow,
        static pending => new ValueOwnerKey(pending.Generation, pending.OwnerId),
        static key => [key.Generation, key.OwnerId.ToString()], []);

    internal static readonly KvDirectoryIndex<RuntimeCriteriaValueUnresolvedIndexRow> ByUnresolvedEdition = new(
        "by_unresolved_edition", 1,
        static unresolved => [unresolved.Generation, unresolved.Intent.EditionId.ToString(),
            unresolved.OutcomeEventId.ToString()]);

    internal static readonly KvDirectory<RuntimeCriteriaValueUnresolvedIndexRow, ValueOutcomeKey> UnresolvedValues = new(
        "runtime_criteria_unresolved_values", ComplianceCoreJsonContext.Default.RuntimeCriteriaValueUnresolvedIndexRow,
        static unresolved => new ValueOutcomeKey(unresolved.Generation, unresolved.OutcomeEventId),
        static key => [key.Generation, key.OutcomeEventId.ToString()], [ByUnresolvedEdition]);

    internal static readonly KvDirectoryIndex<RuntimeCriteriaDisclosureIndexRow> ByEditionDisclosure = new(
        "by_edition_disclosure", 1,
        static disclosure => [disclosure.Generation, disclosure.EditionId.ToString(),
            disclosure.Category, disclosure.Code]);

    internal static readonly KvDirectory<RuntimeCriteriaDisclosureIndexRow, DisclosureKey> Disclosures = new(
        "runtime_criteria_disclosures", ComplianceCoreJsonContext.Default.RuntimeCriteriaDisclosureIndexRow,
        static disclosure => new DisclosureKey(disclosure.Generation, disclosure.EditionId,
            disclosure.Category, disclosure.Code),
        static key => [key.Generation, key.EditionId.ToString(), key.Category, key.Code], [ByEditionDisclosure]);
}
