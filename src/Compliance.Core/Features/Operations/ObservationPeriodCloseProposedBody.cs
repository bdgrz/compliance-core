using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Accepts the exact server-derived close reconciliation and its immutable original result.</summary>
public sealed record ObservationPeriodCloseProposedBody(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid? ActorMemberId, TypeIIPeriodView State, DateTimeOffset OccurredAt, ImmutableContentRef Change,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
   ;
