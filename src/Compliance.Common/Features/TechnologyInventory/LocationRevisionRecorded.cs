using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Applies one complete, attributed location change after its active name was claimed; revision 1 creates it.
///     The complete normalized <see cref="LocationContent" /> is the content behind <paramref name="ContentContent" />.
/// </summary>
[Discriminator("bdgrz.inventory.location.revision_recorded", 1)]
public sealed record LocationRevisionRecorded(Uuid TenantId, Uuid LocationId, long Revision,
    ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordAppliedFact
{
    /// <summary>Schema of the content behind <see cref="ContentContent" />.</summary>
    public const string ContentSchema = "LocationRevisionRecorded.ContentContent.v1";

    Uuid INamedRecordAppliedFact.RecordTenantId => TenantId;

    Uuid INamedRecordAppliedFact.RecordId => LocationId;

    long INamedRecordAppliedFact.RecordRevision => Revision;
}
