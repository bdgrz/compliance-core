using System.Collections.Concurrent;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

/// <summary>Temporary in-process email service for development and automated verification.</summary>
public sealed class MockEmailChallengeDelivery : IEmailChallengeDelivery
{
    readonly ConcurrentDictionary<(Uuid UserId, string EmailAddress), string> _messages = new();

    public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress, string token,
        CancellationToken ct)
    {
        _ = challengeId;
        ct.ThrowIfCancellationRequested();
        _messages[(userId, emailAddress)] = token;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress,
        string token, CancellationToken ct) => SendAsync(challengeId, userId, emailAddress, token, ct);

    public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
        string emailAddress, CancellationToken ct)
    {
        _ = challengeId;
        _ = userId;
        _ = emailAddress;
        ct.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public bool TryGetLatest(Uuid userId, string emailAddress, out string? token) =>
        _messages.TryGetValue((userId, emailAddress), out token);
}
