using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Contracts;

/// <summary>Durable CAS store of the technical delivery state of email challenges, keyed by challenge.</summary>
public interface IEmailChallengeDeliveryLedger
{
    /// <summary>Reads the delivery record, or null when no attempt was ever opened.</summary>
    Task<EmailChallengeDeliveryRecord?> ReadAsync(Uuid challengeId, CancellationToken ct = default);

    /// <summary>
    ///     Opens the next attempt durably, before the relay is called. A delivered, failed or already open record is
    ///     returned unchanged, so a resumed step names the attempt it already opened.
    /// </summary>
    Task<EmailChallengeDeliveryRecord> OpenAttemptAsync(Uuid challengeId, string purpose, DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>Applies the outcome of one attempt by CAS and returns the decision and the stored record.</summary>
    Task<EmailChallengeOutcomeDecision> RecordOutcomeAsync(Uuid challengeId, Uuid attemptId, string outcome,
        string? failureCode, Uuid commandId, DateTimeOffset at, CancellationToken ct = default);
}
