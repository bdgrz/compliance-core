using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Kernel.Services;

/// <summary>SHA-256 content digests rendered as lowercase hexadecimal.</summary>
public static class ContentDigest
{
    /// <summary>Digest of raw bytes.</summary>
    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Digest of the value serialized with the source-generated serializer context.</summary>
    public static string Sha256(object value, Type type)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(type);
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(type)
            ?? throw new InvalidOperationException($"No serialization metadata is registered for {type.FullName}.");
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(value, info));
    }

    /// <summary>Digest of the value serialized with the source-generated serializer context.</summary>
    public static string OfJson<T>(T value) where T : notnull => Sha256(value, typeof(T));
}
