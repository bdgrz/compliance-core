using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
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
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.SourceFacts.Models;

/// <summary>
/// Server-internal proof that a concrete native event was re-read and matched. Only this assembly can construct it,
/// so a request cannot manufacture one.
/// </summary>
/// <typeparam name="TFacts">The verified facts carried by the proof.</typeparam>
public sealed record VerifiedSourceProof<TFacts>
{
    internal VerifiedSourceProof(EventStreamAddress source, Uuid eventId, ulong physicalPosition,
        ulong ownerRevision, string payloadSha256, TFacts facts, ulong globalPosition, ulong realmPosition)
    {
        Source = source;
        EventId = eventId;
        PhysicalPosition = physicalPosition;
        OwnerRevision = ownerRevision;
        PayloadSha256 = payloadSha256;
        Facts = facts;
        GlobalPosition = globalPosition;
        RealmPosition = realmPosition;
    }

    /// <summary>The native stream.</summary>
    public EventStreamAddress Source { get; }

    /// <summary>The event id.</summary>
    public Uuid EventId { get; }

    /// <summary>The resource offset.</summary>
    public ulong PhysicalPosition { get; }

    /// <summary>The metadata replay version, distinct from any public logical revision.</summary>
    public ulong OwnerRevision { get; }

    /// <summary>SHA-256 over the concrete serialized payload.</summary>
    public string PayloadSha256 { get; }

    /// <summary>The verified facts.</summary>
    public TFacts Facts { get; }

    /// <summary>The global offset.</summary>
    public ulong GlobalPosition { get; }

    /// <summary>The realm offset.</summary>
    public ulong RealmPosition { get; }
}
