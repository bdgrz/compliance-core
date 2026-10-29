using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The complete frozen inputs of one historical client independence preview. Admission captures the exact service
///     epoch with every service of that epoch, the exact rule version content and its digest; the child preview reads
///     only this snapshot. <paramref name="HistoricalPracticeFacts" /> locates the permanent practice facts the
///     preview relied on and <paramref name="SourceCuts" /> records the committed frontier of each source owner read.
/// </summary>
/// <remarks>
///     Services are stored as complete <see cref="NonattestServiceView" /> items, a superset of the policy records, so
///     the preview can retain the original attribution. Until the fixed-cut reader exists a cut records the committed
///     stream position of its source owner, which the hydrated owner proves, rather than a global watermark.
/// </remarks>
public sealed record ClientIndependencePreviewIntent(DurableIntentHeader Header, Uuid EvaluationId,
    Uuid ClientTenantId, ulong ServiceEpoch, long RuleSetVersion, DateOnly ExaminationPeriodStart,
    ImmutableSelectionManifest CompleteServices, ImmutableContentRef RuleContent,
    ImmutableSelectionManifest HistoricalPracticeFacts, ImmutableSelectionManifest SourceCuts,
    string RuleVersionDigest)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "client-independence-preview-intent.v1";

    /// <summary>Schema of the rule version content.</summary>
    public const string RuleSchema = "independence-rule-version-view.v1";

    /// <summary>Item schema of the complete service selection.</summary>
    public const string ServiceItemSchema = "nonattest-service-view.v1";

    /// <summary>Item schema of the historical practice fact selection.</summary>
    public const string PracticeItemSchema = "source-fact-locator.v1";

    /// <summary>Item schema of the source cut selection.</summary>
    public const string CutItemSchema = "authoritative-read-cut.v1";
}
