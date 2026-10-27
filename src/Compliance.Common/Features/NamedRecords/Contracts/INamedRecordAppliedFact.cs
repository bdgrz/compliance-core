using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.NamedRecords.Contracts;

/// <summary>The shared shape of the facts that apply an accepted change to a named record.</summary>
public interface INamedRecordAppliedFact : INativeOperationFact
{
    /// <summary>The tenant of the record.</summary>
    Uuid RecordTenantId { get; }

    /// <summary>The identity of the record.</summary>
    Uuid RecordId { get; }

    /// <summary>The logical revision the record reaches with this fact; one for the creating fact.</summary>
    long RecordRevision { get; }

    /// <summary>The complete normalized content of the record at <see cref="RecordRevision" />.</summary>
    ImmutableContentRef ContentContent { get; }

    /// <summary>The frozen <see cref="RecordNameChangeIntent" /> the fact applies.</summary>
    ImmutableContentRef Intent { get; }

    /// <summary>The original actor of the change.</summary>
    ActorReference Actor { get; }

    /// <summary>When the change was accepted.</summary>
    DateTimeOffset ChangedAt { get; }
}
