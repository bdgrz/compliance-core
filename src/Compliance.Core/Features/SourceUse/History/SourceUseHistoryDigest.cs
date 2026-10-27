using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.SourceUse.History;

/// <summary>
/// A bounded rolling commitment to the actual ordered payloads and immutable metadata identity seen by replay.
/// Save attribution is stamped after RaiseEvent and is checked on selected actual facts, not mixed into this cut.
/// </summary>
static class SourceUseHistoryDigest
{
    public static string Empty { get; } = new('0', 64);

    public static string Append(string previous, DomainEvent fact)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(hash, Encoding.UTF8.GetBytes(previous));
        Add(hash, Encoding.UTF8.GetBytes(Bdgrz.Compliance.Features.SourceFacts.History.EventKind.Of(fact)));
        Add(hash, JsonSerializer.SerializeToUtf8Bytes(fact,
            ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())!));
        var metadata = fact.Metadata;
        Add(hash, Encoding.UTF8.GetBytes(metadata.EventId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(metadata.AggregateId.ToString()));
        Add(hash, Encoding.UTF8.GetBytes(metadata.AggregateVersion.ToString(CultureInfo.InvariantCulture)));
        Add(hash, Encoding.UTF8.GetBytes(metadata.OccurredOn.ToString("O", CultureInfo.InvariantCulture)));
        Add(hash, [metadata.IsAudit ? (byte)1 : (byte)0]);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static void Add(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
