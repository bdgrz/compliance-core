using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

/// <summary>
///     Applies one complete, attributed operational process change after its active name was claimed; revision 1
///     creates it. The complete normalized <see cref="OperationalProcessContent" /> is the content behind
///     <paramref name="ContentContent" />.
/// </summary>
[Discriminator("bdgrz.inventory.operational_process.revision_recorded", 1)]
public sealed record OperationalProcessRevisionRecorded(Uuid TenantId, Uuid OperationalProcessId, long Revision,
    ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordAppliedFact
{
    /// <summary>Schema of the content behind <see cref="ContentContent" />.</summary>
    public const string ContentSchema = "OperationalProcessRevisionRecorded.ContentContent.v1";

    Uuid INamedRecordAppliedFact.RecordTenantId => TenantId;

    Uuid INamedRecordAppliedFact.RecordId => OperationalProcessId;

    long INamedRecordAppliedFact.RecordRevision => Revision;
}
