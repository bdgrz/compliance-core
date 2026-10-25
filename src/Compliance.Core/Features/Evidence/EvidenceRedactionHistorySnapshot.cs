using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Complete transient lineage; no command owner retains or caps these rows.</summary>
sealed record EvidenceRedactionHistorySnapshot(Uuid TenantId, Uuid RedactionId, long Revision,
    IReadOnlyList<DomainEvent> Facts, IReadOnlyList<EvidenceRedactionPreparationFact> Preparations,
    IReadOnlyList<EvidenceRedactionApprovalFact> Approvals, int LegacyFactCount)
{
    public EvidenceRedactionPreparationFact? CurrentPreparation => Preparations.Count == 0 ? null : Preparations[^1];
    public EvidenceRedactionApprovalFact? CurrentApproval => Approvals.LastOrDefault(value => value.PreparationId == CurrentPreparation?.PreparationId);

    public EvidenceRedactionView View(string originalState, string derivedState, bool current) =>
        new(TenantId, RedactionId, Revision,
            Array.AsReadOnly(Preparations.Select(value => new EvidenceRedactionPreparationView(value.PreparationId, value.Revision,
                PublicIdentity(value.Original), PublicIdentity(value.Derived), value.Provenance, value.Reason,
                value.PreparedBy, value.PreparedAt)).ToArray()),
            Array.AsReadOnly(Approvals.Select(value => new EvidenceRedactionApprovalView(value.ApprovalId, value.Revision,
                value.PreparationId, value.PreparedRevision, value.ApprovedBy, value.ApprovedAt,
                value.SeparationOfDutiesWaiver is not null, value.SeparationOfDutiesWaiver?.WaiverId)).ToArray()),
            originalState, derivedState, current);

    public EvidenceRedactionHistorySnapshot With(EvidenceRedactionPreparationFact value) =>
        this with { Revision = value.Revision, Preparations = Array.AsReadOnly(Preparations.Append(value).ToArray()) };
    public EvidenceRedactionHistorySnapshot With(EvidenceRedactionApprovalFact value) =>
        this with { Revision = value.Revision, Approvals = Array.AsReadOnly(Approvals.Append(value).ToArray()) };
    static EvidenceRedactionArtifactView PublicIdentity(EvidenceRedactionSourceCapsule source) =>
        new(source.ArtifactId, source.ContentSha256, source.ContentLength, source.RegisteredAt);
}
