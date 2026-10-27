using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Content.Models;

/// <summary>Versioned persistent AES keys; retain old versions for every supported original-result replay.</summary>
public sealed class ProtectedResponseKeyRing
{
    readonly Dictionary<string, byte[]> _keys;
    internal string? ActiveKeyVersionId { get; }

    /// <summary>Copies configured key material without exposing it through diagnostic record formatting.</summary>
    public ProtectedResponseKeyRing(string? activeKeyVersionId, IReadOnlyDictionary<string, byte[]> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (activeKeyVersionId is not null)
            ValidateVersion(activeKeyVersionId);
        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (version, key) in keys)
        {
            ValidateVersion(version);
            if (key is null || key.Length != 32)
                throw new InvalidOperationException("Every protected response key must contain exactly 32 bytes.");
            _keys.Add(version, (byte[])key.Clone());
        }
        if (activeKeyVersionId is not null && !_keys.ContainsKey(activeKeyVersionId))
            throw new InvalidOperationException("The active protected response key version is not configured.");
        ActiveKeyVersionId = activeKeyVersionId;
    }

    /// <summary>Reads ProtectedResponses:ActiveKeyVersionId and base64 values under ProtectedResponses:Keys.</summary>
    public static ProtectedResponseKeyRing FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection("ProtectedResponses:Keys").GetChildren())
        {
            try
            {
                keys.Add(entry.Key, Convert.FromBase64String(entry.Value ?? string.Empty));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("A protected response key is not valid base64.");
            }
        }
        return new ProtectedResponseKeyRing(configuration["ProtectedResponses:ActiveKeyVersionId"], keys);
    }

    internal byte[]? Key(string version) => _keys.TryGetValue(version, out var key) ? key : null;

    static void ValidateVersion(string value)
    {
        if (value.Length is < 1 or > 128 || value.Any(static c =>
                !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new InvalidOperationException("A protected response key version must be a bounded opaque identifier.");
    }
}
