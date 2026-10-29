using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Events;

/// <summary>A member's authored preference receipt, including an accepted no-change decision.</summary>
[Discriminator("bdgrz.work.digest_preference.recorded", 1)]
public sealed record WorkDigestPreferenceRecorded(Uuid TenantId, Uuid MemberId,
    bool EmailDigestEnabled, string? AuthoredTimeZoneId, bool NoChange,
    WorkDigestPreferenceView Preference, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
