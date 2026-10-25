using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>A member's authored preference receipt, including an accepted no-change decision.</summary>
[Discriminator("bdgrz.work.digest_preference.recorded", 1)]
public sealed record WorkDigestPreferenceRecorded(Uuid TenantId, Uuid MemberId,
    bool EmailDigestEnabled, string? AuthoredTimeZoneId, bool NoChange,
    WorkDigestPreferenceView Preference, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
