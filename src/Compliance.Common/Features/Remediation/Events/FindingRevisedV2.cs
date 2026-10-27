using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>An attributable severity, ownership, due-date, scope or root-cause change.</summary>
[Discriminator("bdgrz.finding.revised", 3)]
public sealed record FindingRevisedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    string Severity, Uuid OwnerMemberId, DateOnly DueOn, string AffectedScope, string? RootCause,
    string Reason, ActorReference RevisedBy, DateTimeOffset RevisedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
