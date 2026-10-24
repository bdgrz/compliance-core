using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete frozen terms of one personal engagement decision. Admission pins the exact engagement content
///     revision and the exact service epoch with its complete service manifest, freezes the authored content and the
///     exact source uses the kind requires; every receiver copies the header unchanged. The decision is authorized only
///     after all required uses are held and can be canceled only before that.
/// </summary>
/// <remarks>
///     <paramref name="DecisionId" /> is the acknowledgement or evaluation identity. Services are stored as complete
///     <see cref="NonattestServiceView" /> items, a superset of the policy records, so the child can retain the
///     original attribution. <paramref name="RequiredSourceUses" /> lists <see cref="SourceUseRequirement" /> items in
///     canonical stream key order; a management acknowledgement requires only the service history, a partner
///     evaluation additionally the partner's staff record and duty designation and both rule catalogs.
/// </remarks>
public sealed record EngagementPersonalDecisionIntent(DurableIntentHeader Header, Uuid DecisionId,
    EngagementPersonalDecisionKind Kind, Uuid EngagementId, ulong ExpectedEngagementRevision, ulong ServiceEpoch,
    ImmutableSelectionManifest CompleteServiceManifest, EngagementPersonalDecisionContent AuthoredContent,
    ImmutableSelectionManifest RequiredSourceUses)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "engagement-personal-decision-intent.v1";

    /// <summary>Item schema of the complete service selection.</summary>
    public const string ServiceItemSchema = ClientIndependencePreviewIntent.ServiceItemSchema;

    /// <summary>Item schema of the authored service record identity selection.</summary>
    public const string IdItemSchema = "uuid.v1";

    /// <summary>Item schema of the required source use selection.</summary>
    public const string UseItemSchema = SourceUseRequirement.Schema;

    /// <summary>Item schema of the grant proof selection that authorizes the decision.</summary>
    public const string ProofSchema = "engagement-personal-decision-source-use-proofs.v1";

    /// <summary>The client tenant of the engagement, which is the realm of its owner and of the intent content.</summary>
    public static Uuid TenantOf(DurableIntentHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        return Uuid.Parse(header.FirstOwner.Realm, System.Globalization.CultureInfo.InvariantCulture);
    }
}
