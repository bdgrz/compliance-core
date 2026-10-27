using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Applications.Processes;

/// <summary>Deterministic identities, stream keys and provenance of <c>ApplicationImportExecutionProcess</c>.</summary>
static class ApplicationImportEffects
{
    public const string ProcessName = "ApplicationImportExecutionProcess";

    /// <summary>The only source generation of a batch: a failed or canceled batch is never retried under another.</summary>
    public const long Generation = 1;

    /// <summary>Stream area of every canonical import source.</summary>
    public const string SourceArea = ApplicationImportSource.Area;

    /// <summary>Stream area of every declared application target.</summary>
    public const string TargetArea = DeclaredApplication.Area;

    /// <summary>The command id the process driver derives for a receiver step of a root source fact.</summary>
    public static Uuid ReceiverCommandId(Uuid rootSourceEventId, string receiver, string target, string episode) =>
        EffectIdentity.CommandId(rootSourceEventId, $"{ProcessName}:{receiver}", target, episode);

    /// <summary>Provenance of a receiver fact: it copies the root header of the frozen intent.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        ImportFrozenPlanIntent intent, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, intent.Header, owner, acceptedAt);

    /// <summary>Provenance of a target receiver fact: it copies the root header of the frozen plan.</summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt) =>
        SourceUseReceiverSupport.Child(commandId, commandKind, causationEventId, header, owner, acceptedAt);

    /// <summary>The canonical process target key of a declared application, ordered by unsigned UTF-8 bytes.</summary>
    public static string TargetKey(Uuid applicationId) =>
        $"{TargetArea}/{applicationId.ToString()}";

    /// <summary>The canonical process target key of a source stream.</summary>
    public static string SourceKey(EventStreamAddress source) => $"{source.Area}/{source.Resource}";

    /// <summary>Parses a process target key into its area and resource id.</summary>
    public static (string Area, Uuid Resource)? ParseKey(string key)
    {
        var slash = key.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && Uuid.TryParse(key[(slash + 1)..], CultureInfo.InvariantCulture, out var resource)
            ? (key[..slash], resource)
            : null;
    }

    /// <summary>The deterministic identity of a staged row.</summary>
    public static Uuid RowId(Uuid batchId, int rowNumber) =>
        Uuid.CreateVersion5(batchId, rowNumber.ToString(CultureInfo.InvariantCulture));

    /// <summary>The deterministic identity of the application a create-new row declares.</summary>
    public static Uuid CreatedApplicationId(Uuid batchId, Uuid rowId) =>
        Uuid.CreateVersion5(batchId, $"application_import_target:{rowId}");

    /// <summary>The deterministic identity of a retirement row of a source claim.</summary>
    public static Uuid RetirementRowId(Uuid batchId, Uuid claimId) =>
        Uuid.CreateVersion5(batchId, $"application_import_retirement:{claimId}");

    /// <summary>The deterministic identity of the source claim of one source record.</summary>
    public static Uuid SourceClaimId(Uuid sourceId, string sourceRecordId) =>
        Uuid.CreateVersion5(sourceId, $"application_import_claim:{sourceRecordId}");

    /// <summary>The logical id of the provisional effect fact of one row; the first row carries the command id itself.</summary>
    public static Uuid EffectLogicalId(Uuid commandId, int index, Uuid rowId) =>
        index == 0 ? commandId : Uuid.CreateVersion5(commandId, $"application_import_effect:{rowId}");
}
