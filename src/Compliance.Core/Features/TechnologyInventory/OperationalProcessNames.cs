using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using System.Text.Json;
using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>The operational process family of the shared name-change machinery: its descriptor and receiver names.</summary>
public static class OperationalProcessNames
{
    /// <summary>The name kind of process claims; process names are unique tenant-wide.</summary>
    public const string NameKind = "operational_process";

    /// <summary>Catalog name of the receiver that acquires an operational process name claim.</summary>
    public const string ClaimReceiver = "ClaimOperationalProcessName";

    /// <summary>Catalog name of the receiver that applies the accepted operational process change.</summary>
    public const string ApplyReceiver = "ApplyOperationalProcessChange";

    /// <summary>Catalog name of the receiver that releases an operational process name claim.</summary>
    public const string ReleaseReceiver = "ReleaseOperationalProcessName";

    /// <summary>Catalog name of the abort receiver that cancels a pending operational process change.</summary>
    public const string CancelReceiver = "CancelOperationalProcessChange";

    /// <summary>Catalog name of the operational process name-change process.</summary>
    public const string ProcessName = "OperationalProcessNameChangeProcess";

    /// <summary>The descriptor the shared admission, receivers and runner use for operational processes.</summary>
    public static NamedRecordKindDescriptor Descriptor { get; } = new(NamedRecordKind.OperationalProcess,
        "operational process", OperationalProcessNameClaim.OwnerType, OperationalProcessNameClaim.Area,
        "RecordOperationalProcess", "ReviseOperationalProcess", ClaimReceiver, ApplyReceiver, ReleaseReceiver,
        CancelReceiver, "operational-process-content.v1", OperationalProcessRevisionRecorded.ContentSchema,
        OperationalProcessRevisionRecorded.ContentSchema, ProcessName,
        (tenantId, processId, logicalId) => new OperationalProcess(tenantId, processId, logicalId),
        (tenantId, stream, logicalId) => new OperationalProcessNameClaim(tenantId, stream, logicalId),
        ReadName, ResultOf);

    /// <summary>The name a serialized process content claims: only an active process claims.</summary>
    public static NamedContentName ReadName(ReadOnlyMemory<byte> content)
    {
        var process = JsonSerializer.Deserialize(content.Span,
            ComplianceCoreJsonContext.Default.OperationalProcessContent)
            ?? throw new InvalidOperationException("The operational process content is empty.");
        return new NamedContentName(NameKind,
            process.Lifecycle == TechnologyInventoryRules.Active ? NameNormalization.Normalize(process.Name) : null,
            process.Lifecycle);
    }

    static (string Schema, byte[] Bytes) ResultOf(bool create, Uuid processId, long revision) => create
        ? ("operational-process-registration.v1", JsonSerializer.SerializeToUtf8Bytes(
            new OperationalProcessRegistration(processId),
            ComplianceCoreJsonContext.Default.OperationalProcessRegistration))
        : (Bdgrz.Compliance.Features.NativeOperations.Models.UnitResult.Schema, JsonSerializer.SerializeToUtf8Bytes(
            new Bdgrz.Compliance.Features.NativeOperations.Models.UnitResult(), ComplianceCoreJsonContext.Default.UnitResult));
}
