using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

/// <summary>
///     The creating change of a provider was applied after its governed name was claimed. The complete normalized
///     <see cref="ProviderContent" /> is the content behind <paramref name="ContentContent" />.
/// </summary>
[Discriminator("bdgrz.provider.recorded", 1)]
public sealed record ProviderRecorded(Uuid TenantId, Uuid ProviderId, Uuid RequestId,
    ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordAppliedFact
{
    /// <summary>Schema of the content behind <see cref="ContentContent" />.</summary>
    public const string ContentSchema = "ProviderRecorded.ContentContent.v1";

    Uuid INamedRecordAppliedFact.RecordTenantId => TenantId;

    Uuid INamedRecordAppliedFact.RecordId => ProviderId;

    long INamedRecordAppliedFact.RecordRevision => 1;

    DateTimeOffset INamedRecordAppliedFact.ChangedAt => RecordedAt;
}
