using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Validates the complete physical/native lineage and sources while retaining only current/selected owner authority.</summary>
static class EvidenceRedactionHistory
{
    public static async Task<EvidenceRedactionHistorySnapshot> ReadAsync(IDomainEventReader events, EvidenceRedaction owner,
        IImmutableContentStore? content = null, ISourceFactResolver? resolver = null, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var preparations = new List<EvidenceRedactionPreparationFact>();
        var approvals = new List<EvidenceRedactionApprovalFact>();
        var ids = new HashSet<Uuid>();
        var digest = EvidenceRedactionReplayCut.Empty;
        var metadata = EvidenceRedactionReplayCut.Empty;
        var known = EvidenceRedactionReplayCut.Empty;
        var legacyCount = 0;
        var nativeStarted = false;
        EvidenceRedactionPreparationFact? current = null;
        EvidenceRedactionApprovalFact? currentApproval = null;
        DomainEvent? currentPreparationFact = null;
        DomainEvent? selectedLegacy = null;
        DateTimeOffset last = default;
        long revision = 0;
        foreach (var fact in facts)
        {
            digest = EvidenceRedactionReplayCut.Append(digest, fact);
            metadata = EvidenceRedactionReplayCut.Append(metadata, fact, true);
            if (fact.Metadata.AggregateVersion <= owner.ReplayCut.KnownThroughVersion)
                known = EvidenceRedactionReplayCut.Append(known, fact, true);
            var id = fact switch
            {
                EvidenceRedactionPrepared value => value.Preparation.PreparationId,
                EvidenceRedactionApproved value => value.Approval.ApprovalId,
                EvidenceRedactionPreparedV2 value => value.Preparation.PreparationId,
                EvidenceRedactionApprovedV2 value => value.Approval.ApprovalId,
                _ => throw Invalid("The redaction prefix contains an unsupported fact type/version."),
            };
            if (id == Uuid.Empty || !ids.Add(id))
                throw Invalid("The complete redaction prefix repeats an operation across preparation/approval kinds.");
            if (fact is INativeOperationFact native)
            {
                nativeStarted = true;
                var kind = fact is EvidenceRedactionPreparedV2 ? nameof(PrepareEvidenceRedaction) : nameof(ApproveEvidenceRedaction);
                if (native.Operation.LogicalId != id || native.FirstResponse is null || EvidenceRedaction.OperationResultOf(fact) is not { Schema: "mutation-result-content.v1" } ||
                    !NativeOwnerHistory.IsOwnRoot(owner, native.Operation, kind, (ulong)revision))
                    throw Invalid("The native redaction receipt differs from its exact root, actor, owner or original revision.");
                if (id == owner.Acceptance.SelectedLogicalId && (owner.Acceptance.SelectedFact is not { } selected ||
                    selected.GetType() != fact.GetType() || !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                    throw Invalid("The selected receipt differs from its full actual native payload and metadata.");
            }
            else
            {
                if (nativeStarted)
                    throw Invalid("An occupied legacy redaction fact cannot follow native v2 admission.");
                legacyCount++;
                if (id == owner.Acceptance.SelectedLogicalId)
                    selectedLegacy = fact;
            }
            switch (fact)
            {
                case EvidenceRedactionPrepared value:
                    Prepare(value.TenantId, value.RedactionId, value.ExpectedRevision, value.Preparation, fact);
                    break;
                case EvidenceRedactionPreparedV2 value:
                    Prepare(value.TenantId, value.RedactionId, value.ExpectedRevision, value.Preparation, fact);
                    if (value.Operation.Actor != value.Preparation.PreparedBy || value.Operation.AcceptedAt != value.Preparation.PreparedAt ||
                        value.Operation.AuthoredIntentSha256 != AuthoredIntentDigest.Compute(new PrepareEvidenceRedaction(value.Preparation.PreparationId,
                            value.TenantId, value.RedactionId, value.ExpectedRevision, value.Preparation.Original.ArtifactId,
                            value.Preparation.Derived.ArtifactId, value.Preparation.Provenance, value.Preparation.Reason)))
                        throw Invalid("The native preparation differs from its original canonical actor and admission time.");
                    await Proofs().VerifyAsync(owner, value.SourceProofs, value.Preparation, null, null, ct).ConfigureAwait(false);
                    break;
                case EvidenceRedactionApproved value:
                    Approve(value.TenantId, value.RedactionId, value.ExpectedRevision, value.Approval);
                    break;
                case EvidenceRedactionApprovedV2 value:
                    var prepared = current ?? throw Invalid("A native approval has no exact current preparation.");
                    var preparedSource = currentPreparationFact ?? throw Invalid("A native approval has no exact preparation source record.");
                    Approve(value.TenantId, value.RedactionId, value.ExpectedRevision, value.Approval);
                    if (value.Operation.Actor != value.Approval.ApprovedBy || value.Operation.AcceptedAt != value.Approval.ApprovedAt ||
                        value.Operation.AuthoredIntentSha256 != AuthoredIntentDigest.Compute(new ApproveEvidenceRedaction(value.Approval.ApprovalId,
                            value.TenantId, value.RedactionId, value.ExpectedRevision, value.Approval.PreparationId,
                            value.Approval.PreparedRevision, value.Approval.SeparationOfDutiesWaiver?.WaiverId)))
                        throw Invalid("The native approval differs from its original canonical actor and admission time.");
                    await Proofs().VerifyAsync(owner, value.SourceProofs, prepared, value.Approval, preparedSource, ct).ConfigureAwait(false);
                    break;
                default:
                    throw Invalid("The redaction prefix contains another fact kind.");
            }
            if (!EvidenceRedactionRules.Fits(fact))
                throw Invalid("A retained redaction fact exceeds its complete typed frame bound.");
        }
        if (owner.Revision != revision || owner.CurrentPreparation != current || owner.CurrentApproval != currentApproval ||
            owner.CurrentPreparationFact != currentPreparationFact || owner.LastChangedAt != last ||
            owner.HasSelectedLegacy != (selectedLegacy is not null) ||
            selectedLegacy is not null && (owner.SelectedLegacyFact is not { } actualLegacy || actualLegacy != selectedLegacy || actualLegacy.Metadata != selectedLegacy.Metadata))
            throw Invalid("The full redaction prefix differs from its compact current/selected authority.");
        owner.ReplayCut.Confirm(owner, digest, metadata, known);
        return new EvidenceRedactionHistorySnapshot(owner.TenantId, owner.Id, revision, facts,
            Array.AsReadOnly(preparations.ToArray()), Array.AsReadOnly(approvals.ToArray()), legacyCount);

        EvidenceRedactionProofs Proofs() => content is not null && resolver is not null
            ? new EvidenceRedactionProofs(events, resolver, content)
            : throw Invalid("Native v2 redaction history requires actual immutable content and source verification; legacy facts are not upgraded.");

        void Prepare(Uuid tenantId, Uuid redactionId, long expected, EvidenceRedactionPreparationFact value, DomainEvent fact)
        {
            var decided = EvidenceRedactionRules.Prepare(owner.TenantId, owner.Id, revision, current, last, value.PreparationId,
                expected, value.Original, value.Derived, value.Provenance, value.Reason, value.PreparedBy, value.PreparedAt);
            if (tenantId != owner.TenantId || redactionId != owner.Id || !decided.IsSuccess || decided.Value != value)
                throw Invalid("The preparation differs from its exact source pair, attribution, chronology or predecessor revision.");
            current = value;
            currentApproval = null;
            currentPreparationFact = fact;
            revision = value.Revision;
            last = value.PreparedAt;
            preparations.Add(value);
        }

        void Approve(Uuid tenantId, Uuid redactionId, long expected, EvidenceRedactionApprovalFact value)
        {
            var decided = EvidenceRedactionRules.Approve(owner.TenantId, owner.Id, revision, current, currentApproval, last,
                value.ApprovalId, expected, value.PreparationId, value.PreparedRevision, value.DerivedSourcePosition, value.ApprovedBy,
                value.ApprovedAt, value.DerivedAvailableAt, value.SeparationOfDutiesWaiver);
            if (tenantId != owner.TenantId || redactionId != owner.Id || !decided.IsSuccess || decided.Value != value)
                throw Invalid("The approval differs from its exact current preparation, source chronology or independent value proof.");
            currentApproval = value;
            revision = value.Revision;
            last = value.ApprovedAt;
            approvals.Add(value);
        }
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
