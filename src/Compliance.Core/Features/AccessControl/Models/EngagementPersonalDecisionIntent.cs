using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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
