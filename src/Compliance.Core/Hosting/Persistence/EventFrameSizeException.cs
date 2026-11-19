using System.Globalization;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>A complete attributed batch was rejected before its append session opened.</summary>
sealed class EventFrameSizeException(long operationBytes, int maximumBytes = ushort.MaxValue)
    : Exception(maximumBytes == ushort.MaxValue
        ? "An attributed event exceeds the 65,535-byte storage operation limit."
        : $"An attributed event exceeds the {maximumBytes.ToString(CultureInfo.InvariantCulture)}-byte storage operation limit.")
{
    public long OperationBytes { get; } = operationBytes;
    public int MaximumBytes { get; } = maximumBytes;
}
