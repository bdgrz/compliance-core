using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Bounded current lineage decisions; source authentication and waiver eligibility are independently prepared values.</summary>
static class EvidenceRedactionRules
{
    public static Result<EvidenceRedactionPreparationFact> Prepare(Uuid tenantId, Uuid redactionId, long revision,
        EvidenceRedactionPreparationFact? current, DateTimeOffset lastChangedAt, Uuid requestId, long expectedRevision,
        EvidenceRedactionSourceCapsule original, EvidenceRedactionSourceCapsule derived, string provenance,
        string reason, ActorReference actor, DateTimeOffset at)
    {
        provenance = provenance?.Trim() ?? "";
        reason = reason?.Trim() ?? "";
        if (!ValidIdentity(tenantId, redactionId, original) || !ValidIdentity(tenantId, redactionId, derived) ||
            original.ArtifactId == derived.ArtifactId || original.ContentSha256 == derived.ContentSha256 ||
            !ValidActor(actor) || requestId == Uuid.Empty || provenance.Length is 0 or > 4000 || reason.Length is 0 or > 4000)
            return Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Validation,
                "Redaction preparation requires distinct immutable artifact identities, attribution and bounded provenance/reason.");
        if (expectedRevision != revision || current is not null && (current.Original != original || current.Derived != derived) ||
            at == default || at < original.RegisteredAt || at < derived.RegisteredAt || at < lastChangedAt)
            return Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Conflict,
                "Reload the exact source-bound redaction revision and preserve source chronology.");
        var prepared = new EvidenceRedactionPreparationFact(requestId, revision + 1, original, derived, provenance, reason, actor, at);
        return Fits(new EvidenceRedactionPrepared(tenantId, redactionId, revision, prepared))
            ? Result<EvidenceRedactionPreparationFact>.Success(prepared)
            : Refuse<EvidenceRedactionPreparationFact>(RequestErrorKind.Validation, "The complete redaction event exceeds its payload bound.");
    }

    public static Result<EvidenceRedactionApprovalFact> Approve(Uuid tenantId, Uuid redactionId, long revision,
        EvidenceRedactionPreparationFact? prepared, EvidenceRedactionApprovalFact? currentApproval, DateTimeOffset lastChangedAt,
        Uuid requestId, long expectedRevision, Uuid preparationId, long preparedRevision, ulong derivedPosition,
        ActorReference actor, DateTimeOffset at, DateTimeOffset availableAt, EvidenceRedactionWaiverSnapshot? waiver)
    {
        if (!ValidActor(actor) || requestId == Uuid.Empty)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Validation, "Approval requires a canonical attributed member decision.");
        if (expectedRevision != revision || prepared is null || prepared.PreparationId != preparationId ||
            prepared.Revision != preparedRevision || currentApproval is not null || derivedPosition < 2 ||
            at == default || at < lastChangedAt || availableAt == default || availableAt < prepared.Derived.RegisteredAt || at < availableAt)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Conflict,
                "Approve the current exact preparation and preserve source chronology.");
        if (waiver is not null && !ValidWaiver(tenantId, redactionId, waiver, prepared, actor, at) ||
            SameActor(prepared.PreparedBy, actor) && waiver is null)
            return Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Forbidden,
                "A preparer cannot approve their own derivative without an independently approved exact-scope active waiver.");
        var approval = new EvidenceRedactionApprovalFact(requestId, revision + 1, preparationId, preparedRevision,
            derivedPosition, availableAt, actor, at, waiver);
        return Fits(new EvidenceRedactionApproved(tenantId, redactionId, revision, approval))
            ? Result<EvidenceRedactionApprovalFact>.Success(approval)
            : Refuse<EvidenceRedactionApprovalFact>(RequestErrorKind.Validation, "The complete approval event exceeds its payload bound.");
    }

    public static bool ValidWaiver(Uuid tenantId, Uuid redactionId, EvidenceRedactionWaiverSnapshot waiver,
        EvidenceRedactionPreparationFact prepared, ActorReference actor, DateTimeOffset at) =>
        waiver.TenantId == tenantId && waiver.WaiverId != Uuid.Empty && waiver.Scope == WaiverScope(redactionId, prepared) &&
        waiver.BeneficiaryMemberId.ToString() == actor.Id && waiver.RequesterMemberId != Uuid.Empty && waiver.ApproverMemberId != Uuid.Empty &&
        waiver.ApproverMemberId != waiver.BeneficiaryMemberId && waiver.ApproverMemberId != waiver.RequesterMemberId &&
        waiver.RequestedAt >= prepared.PreparedAt && waiver.ApprovedAt >= waiver.RequestedAt && at >= waiver.ApprovedAt && at < waiver.ExpiresAt;

    public static SeparationOfDutiesWaiverScope WaiverScope(Uuid redactionId, EvidenceRedactionPreparationFact prepared) =>
        new(SeparationOfDutiesRecordTypes.EvidenceRedaction, redactionId, prepared.PreparationId, prepared.Revision, SeparationOfDutiesActions.Approve);

    public static bool SameActor(ActorReference first, ActorReference second) => first.Kind == second.Kind && first.Id == second.Id;

    public static bool ValidIdentity(Uuid tenantId, Uuid redactionId, EvidenceRedactionSourceCapsule? source) =>
        tenantId != Uuid.Empty && redactionId != Uuid.Empty && source is not null && source.TenantId == tenantId &&
        source.RegistrationEventId != Uuid.Empty && ValidDigest(source.ContentSha256) && ValidDigest(source.RegistrationSha256) &&
        source.ContentLength > 0 && source.ArtifactId != Uuid.Empty && source.RegisteredAt != default && source.IdentitySha256 == IdentityDigest(source);

    public static string IdentityDigest(EvidenceRedactionSourceCapsule source) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source with { IdentitySha256 = "" },
            typeof(EvidenceRedactionSourceCapsule), ComplianceCoreJsonContext.Default)));

    public static bool ValidActor(ActorReference? actor) => actor is { Kind: "member", Display.Length: > 0 and <= 200 } &&
        Uuid.TryParse(actor.Id, out var id) && id != Uuid.Empty;
    public static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, value!.GetType(), ComplianceCoreJsonContext.Default).Length <= 48 * 1024;
    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    static Result<T> Refuse<T>(RequestErrorKind kind, string message) => Result<T>.Failure(new RequestError(kind, message));
}
