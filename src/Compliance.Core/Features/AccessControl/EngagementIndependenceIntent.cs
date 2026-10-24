using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete frozen terms of one engagement acceptance or positive staff assignment. Admission pins the exact
///     engagement content revision, the exact service epoch with its complete service manifest, the paired personal
///     decisions and the complete manifests of every required source use and every practice subject; every receiver
///     copies the header unchanged. All temporary grants derive from this original admission and are acquired in
///     canonical stream key order, with no epoch-before-practice dependency; only the irreversible authorization
///     validates the complete grant set and permits any permanent commitment.
/// </summary>
/// <remarks>
///     <paramref name="Evidence" /> is the complete <see cref="EngagementAcceptanceEvidence" /> the operation relies on.
///     <paramref name="SourceUses" /> lists <see cref="SourceUseRequirement" /> items and <paramref name="Subjects" />
///     lists <see cref="PracticeSubjectIntent" /> items, both in canonical stream key order.
/// </remarks>
public sealed record EngagementIndependenceIntent(DurableIntentHeader Header, Uuid EngagementId,
    ulong EngagementRevision, EngagementIndependenceOperationKind OperationKind, ulong ServiceEpoch,
    ImmutableSelectionManifest CompleteServiceHistory, ImmutableContentRef Evidence,
    NativeFactReference? PartnerDecision, NativeFactReference? ManagementAcknowledgement,
    ImmutableSelectionManifest Subjects, ImmutableSelectionManifest SourceUses, Uuid? StaffMemberId,
    DateOnly? EffectiveFrom)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "engagement-independence-intent.v1";

    /// <summary>Item schema of the practice subject selection.</summary>
    public const string SubjectItemSchema = PracticeSubjectIntent.Schema;

    /// <summary>Item schema of the required source use selection.</summary>
    public const string UseItemSchema = SourceUseRequirement.Schema;

    /// <summary>Item schema of the complete service selection.</summary>
    public const string ServiceItemSchema = ClientIndependencePreviewIntent.ServiceItemSchema;

    /// <summary>Schema of the evidence content.</summary>
    public const string EvidenceSchema = "engagement-acceptance-evidence.v1";

    /// <summary>Schema of the grant proof manifests that authorize, commit and complete the operation.</summary>
    public const string ProofSchema = "engagement-independence-proofs.v1";

    /// <summary>The client tenant of the engagement, which is the realm of its owner and of the intent content.</summary>
    public static Uuid TenantOf(DurableIntentHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return Uuid.Parse(header.FirstOwner.Realm, System.Globalization.CultureInfo.InvariantCulture);
    }
}
