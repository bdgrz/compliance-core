using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The materialization of an admitted sample selection on its own owner, at revision one. <paramref name="Change" />
///     repeats the frozen <c>AuditorSampleSelectionIntent</c>, so the selection never rereads the request or the latest
///     population.
/// </summary>
[Discriminator("bdgrz.auditorrequests.sample-selection-recorded", 1)]
public sealed record SampleSelectionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ImmutableContentRef Change, DateTimeOffset OccurredAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
