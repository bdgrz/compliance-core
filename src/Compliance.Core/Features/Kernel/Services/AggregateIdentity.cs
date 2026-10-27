using System.Buffers.Binary;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
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
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Kernel.Services;

/// <summary>Deterministic UUIDv5 aggregate identities over typed, length-prefixed parts.</summary>
public static class AggregateIdentity
{
    /// <summary>Identity namespace for all aggregate identities.</summary>
    public static readonly Uuid Namespace = Uuid.Parse("9a7caa76-7d3a-5a9c-9370-7f2d9cc2ecf8",
        System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Creates the identity of an owner type and its string or <see cref="Uuid"/> parts.</summary>
    public static Uuid Create(string ownerType, params object[] parts) =>
        Uuid.CreateVersion5(Namespace, Encode(ownerType, parts));

    /// <summary>Encodes the owner type and parts: strings as 4-byte big-endian length plus UTF-8, UUIDs as 16 bytes.</summary>
    public static byte[] Encode(string ownerType, params object[] parts)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(parts);
        using var buffer = new MemoryStream();
        WriteString(buffer, ownerType);
        foreach (var part in parts)
        {
            switch (part)
            {
                case string text:
                    WriteString(buffer, text);
                    break;
                case Uuid id:
                    buffer.Write(id.ToGuid().ToByteArray(bigEndian: true));
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported identity part type '{part?.GetType().FullName ?? "null"}'.", nameof(parts));
            }
        }

        return buffer.ToArray();
    }

    static void WriteString(MemoryStream buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        buffer.Write(length);
        buffer.Write(bytes);
    }
}
