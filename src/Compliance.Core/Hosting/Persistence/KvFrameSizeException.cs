using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>A KV operation cannot be represented by the pinned Fitz wire protocol.</summary>
sealed class KvFrameSizeException(long operationBytes)
    : Exception("A key/value operation exceeds the 65,535-byte storage operation limit.")
{
    public long OperationBytes { get; } = operationBytes;
}
