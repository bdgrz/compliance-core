using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Opens one bounded advisor handoff. <paramref name="RecordVersionsManifest" /> is the complete exact selection
///     as verified content (<c>AdvisorHandoffCreated.RecordVersionsManifest.v1</c>); readers verify it before use.
/// </summary>
[Discriminator("bdgrz.advisor-handoff.created", 2)]
public sealed record AdvisorHandoffCreated(
    Uuid TenantId, Uuid ProgramId, Uuid HandoffId, ImmutableContentRef RecordVersionsManifest,
    ImmutableContentRef Authored, DateOnly ExpiresOn, ActorReference CreatedBy, DateTimeOffset CreatedAt,
    Uuid? ComparedToHandoffId, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
