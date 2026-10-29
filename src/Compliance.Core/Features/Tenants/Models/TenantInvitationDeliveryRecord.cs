using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     The technical state of the delivery process of one invitation, keyed by tenant and invitation id: every
///     provider attempt, its result and the terminal outcome. It never enters a domain event stream, and the
///     bearer token never enters it.
/// </summary>
public sealed record TenantInvitationDeliveryRecord(Uuid TenantId, Uuid InvitationId, string Status,
    IReadOnlyList<TenantInvitationDeliveryAttempt> Attempts, long Version)
{
    /// <summary>No terminal outcome has been recorded.</summary>
    public const string Pending = "pending";

    /// <summary>The provider accepted the attempt.</summary>
    public const string Delivered = "delivered";

    /// <summary>The attempt definitely failed and the invitation needs an explicit new invitation.</summary>
    public const string Failed = "failed";

    /// <summary>A new record without attempts.</summary>
    public static TenantInvitationDeliveryRecord Start(Uuid tenantId, Uuid invitationId) =>
        new(tenantId, invitationId, Pending, [], 0);

    /// <summary>True once the outcome of the delivery is recorded.</summary>
    public bool IsTerminal => Status is Delivered or Failed;

    /// <summary>The identity of the single attempt of an invitation; a resumed send reuses it.</summary>
    public static Uuid AttemptIdFor(Uuid invitationId) =>
        Uuid.CreateVersion5(invitationId, "tenant-invitation-delivery-attempt:1");

    /// <summary>The recorded attempt, if the send began.</summary>
    public TenantInvitationDeliveryAttempt? Attempt(Uuid attemptId) =>
        Attempts.FirstOrDefault(attempt => attempt.AttemptId == attemptId);

    /// <summary>A copy with the attempt begun, or this record when it already exists.</summary>
    public TenantInvitationDeliveryRecord Begin(Uuid attemptId, DateTimeOffset startedAt) =>
        Attempt(attemptId) is not null
            ? this
            : this with
            {
                Attempts = [.. Attempts, new TenantInvitationDeliveryAttempt(attemptId, startedAt,
                    TenantInvitationDeliveryAttempt.InFlight, null, null)],
            };

    /// <summary>A copy with the provider result stored on the attempt.</summary>
    public TenantInvitationDeliveryRecord WithResult(Uuid attemptId, string result, string? failureCode,
        DateTimeOffset at) =>
        this with
        {
            Attempts = [.. Attempts.Select(attempt => attempt.AttemptId == attemptId
                ? attempt with { Result = result, FailureCode = failureCode, ResultAt = at }
                : attempt)],
        };
}
