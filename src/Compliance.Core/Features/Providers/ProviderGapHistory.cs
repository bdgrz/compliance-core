using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Validates the complete native gap prefix transiently and retains only explicitly selected authority.</summary>
static class ProviderGapHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(ProviderAssuranceGap owner, IDomainEventReader events,
        CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var digest = ProviderGapReplayDigest.Empty;
        var logicalIds = new HashSet<Uuid>();
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        ProviderCoverageGapRecorded? recorded = null;
        ProviderCoverageGapClosed? closed = null;
        ProviderCoverageGapRiskAcceptanceAdmitted? selected = null;
        var revision = 0L;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact { Operation: { } operation } || operation.LogicalId == Uuid.Empty ||
                !logicalIds.Add(operation.LogicalId) || operation.LogicalId != operation.RootOperationId ||
                operation.Owner != owner.Stream || operation.AffectedOwner != owner.Stream ||
                operation.ExpectedRevisionOwner != owner.Stream || (operation.FirstOwner ?? operation.Owner) != owner.Stream ||
                operation.CausationEventId is not null || !Actor(operation.Actor) || operation.AcceptedAt == default ||
                operation.AuthoredIntentSha256 is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit))
                throw Invalid("The provider gap prefix has another root owner, malformed header or duplicate logical operation.");
            digest = ProviderGapReplayDigest.Append(digest, fact);
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId && !Same(owner.Acceptance.SelectedFact, fact))
                throw Invalid("The selected gap acceptance differs from its actual payload and complete metadata.");
            switch (fact)
            {
                case ProviderCoverageGapRecorded value:
                    if (recorded is not null || revision != 0 || !Scope(owner, value.TenantId, value.ProviderId, value.GapId) ||
                        value.RequestId != owner.GapId || operation.LogicalId != owner.GapId || value.ProviderRevision < 1 ||
                        operation.CommandKind != nameof(RecordProviderCoverageGap) || operation.Episode != "initial" ||
                        operation.OriginalExpectedRevision is not null || value.Actor != operation.Actor ||
                        value.RecordedAt != operation.AcceptedAt || !Reference(value.ContentContent, ProviderAssuranceContent.GapRecordedSchema) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema) ||
                        !Reference(value.OperationResult, ProviderAssuranceContent.GapRegistrationSchema))
                        throw Invalid("A provider gap begins once with its exact scoped native record and original attribution.");
                    recorded = value;
                    revision = 1;
                    break;
                case ProviderCoverageGapClosed value:
                    if (recorded is null || closed is not null || !Scope(owner, value.TenantId, value.ProviderId, value.GapId) ||
                        value.RequestId != operation.LogicalId || value.Revision != revision + 1 ||
                        operation.CommandKind != nameof(CloseProviderCoverageGap) || operation.Episode != "current" ||
                        operation.OriginalExpectedRevision != (ulong)revision || value.Actor != operation.Actor ||
                        value.ClosedAt != operation.AcceptedAt || !Reference(value.ContentContent, ProviderAssuranceContent.GapClosedSchema) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema) ||
                        !Reference(value.OperationResult, ProviderAssuranceContent.GapRegistrationSchema))
                        throw Invalid("The exact open provider gap closes once at its original expected business revision.");
                    closed = value;
                    revision++;
                    break;
                case ProviderCoverageGapRiskAcceptanceAdmitted value:
                    if (recorded is null || closed is not null || value.Owner != owner.Stream || value.Revision != revision + 1 ||
                        value.Generation != value.Revision || operation.CommandKind != nameof(LinkProviderCoverageGapRiskAcceptance) ||
                        operation.OriginalExpectedRevision != (ulong)revision || !Pair(operation.Episode) ||
                        !pairs.Add(operation.Episode) || !Reference(value.Intent, ProviderRiskLinkIntent.Schema) ||
                        !Reference(value.FirstResponse, FirstResponses.Schema))
                        throw Invalid("A link requires a new exact risk/acceptance pair on the open gap at its original revision.");
                    if (operation.LogicalId == owner.SelectedAdmissionId)
                        selected = value;
                    revision++;
                    break;
                default:
                    throw Invalid("The provider gap native prefix contains another owner family.");
            }
        }
        if (owner.ReplayCut.Digest != digest || owner.Revision != revision || !Same(owner.Recorded, recorded) ||
            !Same(owner.Closed, closed) || !Same(owner.SelectedAdmission, selected))
            throw Invalid("The provider gap raw native authority differs from its actual hydrated payload and metadata cut.");
        owner.SelectedPairSeen = owner.SelectedPair is { } pair && pairs.Contains(pair);
        owner.SelectedAdmission = selected;
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return facts;
    }

    internal static bool Pair(string? episode)
    {
        if (string.IsNullOrWhiteSpace(episode))
            return false;
        var parts = episode.Split(':');
        return parts.Length == 2 && Uuid.TryParse(parts[0], CultureInfo.InvariantCulture, out var risk) && risk != Uuid.Empty &&
            Uuid.TryParse(parts[1], CultureInfo.InvariantCulture, out var acceptance) && acceptance != Uuid.Empty &&
            ProviderAssuranceGap.PairEpisode(risk, acceptance) == episode;
    }

    internal static bool Reference(ImmutableContentRef? value, string schema) => value is
    { ContentId: var id, Sha256: { Length: 64 } sha, Bytes: > 0, MediaType: "application/json" } &&
        id != Uuid.Empty && sha.All(Uri.IsHexDigit) && value.Schema == schema;

    static bool Scope(ProviderAssuranceGap owner, Uuid tenant, Uuid provider, Uuid gap) =>
        tenant == owner.TenantId && provider == owner.ProviderId && gap == owner.GapId;

    static bool Actor(ActorReference? actor) => actor is not null && !string.IsNullOrWhiteSpace(actor.Kind) &&
        !string.IsNullOrWhiteSpace(actor.Id) && !string.IsNullOrWhiteSpace(actor.Display);

    static bool Same(DomainEvent? left, DomainEvent? right) => left is null ? right is null : right is not null &&
        left.GetType() == right.GetType() && left.Equals(right) && left.Metadata == right.Metadata;

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
