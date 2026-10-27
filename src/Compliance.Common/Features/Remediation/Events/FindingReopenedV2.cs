using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     A closed finding was reopened. The finding advances to the next generation; admitted actions and their history
///     are kept, and a closure accepted for an earlier generation can no longer close it.
/// </summary>
[Discriminator("bdgrz.finding.reopened", 3)]
public sealed record FindingReopenedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Reason, ActorReference ReopenedBy, DateTimeOffset ReopenedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
