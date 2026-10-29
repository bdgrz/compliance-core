using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.Kernel.Services;

/// <summary>
/// Digest of the authored fields of an intent: the intent is serialized through the source-generated serializer
/// context in declaration order with wire property names, timestamps normalized to UTC and decimals to G29, and the
/// UTF-8 document is hashed. C# type and member names never enter the digest, so ordinary renames keep exact retries
/// comparable; the wire contract is what changes it.
/// </summary>
public static class AuthoredIntentDigest
{
    static readonly ConcurrentDictionary<IJsonTypeInfoResolver, JsonSerializerOptions> Options = new();

    /// <summary>Computes the lowercase SHA-256 hex digest of the canonical encoding of an intent registered in <see cref="ComplianceCoreJsonContext"/>.</summary>
    public static string Compute<T>(T intent) where T : notnull => Compute(intent, ComplianceCoreJsonContext.Default);

    /// <summary>Computes the digest using the serialization metadata of <paramref name="resolver"/>.</summary>
    public static string Compute<T>(T intent, IJsonTypeInfoResolver resolver) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(resolver);
        var options = Options.GetOrAdd(resolver, CreateOptions);
        var info = options.GetTypeInfo(typeof(T));
        return ContentDigest.Sha256(JsonSerializer.SerializeToUtf8Bytes(intent, info));
    }

    static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions(ComplianceCoreJsonContext.Default.Options)
        {
            TypeInfoResolver = resolver,
            WriteIndented = false
        };
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        options.Converters.Add(new G29DecimalJsonConverter());
        options.MakeReadOnly(populateMissingResolver: false);
        return options;
    }
}
