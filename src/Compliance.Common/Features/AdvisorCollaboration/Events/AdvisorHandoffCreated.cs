using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

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
