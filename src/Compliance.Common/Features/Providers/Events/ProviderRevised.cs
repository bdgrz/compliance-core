using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     A later change of a provider was applied after the names it needs were claimed. The complete normalized
///     <see cref="ProviderContent" /> is the content behind <paramref name="ContentContent" />.
/// </summary>
[Discriminator("bdgrz.provider.revised", 1)]
public sealed record ProviderRevised(Uuid TenantId, Uuid ProviderId, Uuid RequestId,
    long Revision, ImmutableContentRef ContentContent, ActorReference Actor, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INamedRecordAppliedFact
{
    /// <summary>Schema of the content behind <see cref="ContentContent" />.</summary>
    public const string ContentSchema = "ProviderRevised.ContentContent.v1";

    Uuid INamedRecordAppliedFact.RecordTenantId => TenantId;

    Uuid INamedRecordAppliedFact.RecordId => ProviderId;

    long INamedRecordAppliedFact.RecordRevision => Revision;

    DateTimeOffset INamedRecordAppliedFact.ChangedAt => RecordedAt;
}
