using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Queries;
using Bdgrz.Compliance.Features.UserIdentities.Reactors;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

/// <summary>
///     The email challenge delivery process: at-least-once delivery of the exact message, every attempt recorded as a
///     typed outcome on the delivery ledger and none as a fact of the address stream.
/// </summary>
public sealed class EmailChallengeDeliveryProcessTests
{
    [Fact]
    public async Task ShouldDeliverTheExactTokenOnceGivenAcceptedSend()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueVerificationAsync();

        // Act
        var process = await host.DriveAsync(record);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        var message = Assert.Single(host.Relay.Accepted);
        Assert.Equal((challengeId, EmailChallengeDeliveryHost.Email, false), (message.ChallengeId, message.Email, message.Recovery));
        Assert.Equal(await host.VerificationTokenAsync(owner, challengeId), message.Token);
        Assert.Equal("delivered", ledger!.Status);
        var attempt = Assert.Single(ledger.Attempts);
        Assert.Equal(EmailChallengeDeliveryAttempt.Sent, attempt.Outcome);
        Assert.DoesNotContain(await host.FactsAsync(new EmailAddress(EmailChallengeDeliveryHost.Email).Stream),
            fact => fact.GetType().Name.Contains("Delivery", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldDeliverTheRecoveryCodeGivenRecoveryChallenge()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueRecoveryAsync();
        var issued = (IdentityRecoveryChallengeIssued)record.Event;

        // Act
        var process = await host.DriveAsync(record);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        var message = Assert.Single(host.Relay.Accepted);
        Assert.True(message.Recovery);
        Assert.Equal(host.Keys.DeriveRecovery(issued.TokenKeyId, challengeId, owner, EmailChallengeDeliveryHost.Email,
            issued.ExpiresAt), message.Token);
        Assert.Equal("delivered", (await host.Ledger.ReadAsync(challengeId))!.Status);
    }

    [Fact]
    public async Task ShouldReconcileTheUnknownAttemptBeforeResendingTheSameMessageGivenAmbiguousSmtpResult()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (_, challengeId, record) = await host.IssueVerificationAsync();
        host.Relay.Then(new TimeoutException("connection lost after DATA"));

        // Act
        var first = await host.DriveAsync(record);
        var afterFirst = await host.Ledger.ReadAsync(challengeId);
        var sendsAfterFirst = host.Relay.Sends.Count;
        var resumed = await host.ResumeAsync(challengeId);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Pending, first.State);
        Assert.Equal(1, sendsAfterFirst);
        var unknown = Assert.Single(afterFirst!.Attempts);
        Assert.Equal(EmailChallengeFailureCertainty.Unknown, unknown.Outcome);
        Assert.Equal("transport_ambiguous", unknown.FailureCode);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(2, host.Relay.Sends.Count);
        Assert.Equal(host.Relay.Sends[0], host.Relay.Sends[1]);
        Assert.Equal(2, ledger!.Attempts.Count);
        Assert.Equal(EmailChallengeFailureCertainty.Unknown, ledger.Attempts[0].Outcome);
        Assert.True(ledger.Attempts[0].ClosedAt <= ledger.Attempts[1].StartedAt);
        Assert.Equal(EmailChallengeDeliveryAttempt.Sent, ledger.Attempts[1].Outcome);
        Assert.Equal("delivered", ledger.Status);
    }

    [Fact]
    public async Task ShouldCloseTheOpenAttemptAsUnknownBeforeResendingGivenCrashAfterRelayAcceptance()
    {
        // Arrange
        var crash = new CrashOnSentLedger();
        await using var host = new EmailChallengeDeliveryHost(crash.Wrap);
        var (_, challengeId, record) = await host.IssueVerificationAsync();
        crash.Arm();

        // Act
        var lost = await Assert.ThrowsAsync<InvalidOperationException>(() => host.DriveAsync(record));
        crash.Disarm();
        var afterCrash = await host.Ledger.ReadAsync(challengeId);
        var resumed = await host.ResumeAsync(challengeId);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal("simulated crash", lost.Message);
        Assert.True(Assert.Single(afterCrash!.Attempts).IsOpen);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(2, host.Relay.Accepted.Count);
        Assert.Equal(host.Relay.Accepted[0], host.Relay.Accepted[1]);
        Assert.Equal(2, ledger!.Attempts.Count);
        Assert.Equal(EmailChallengeFailureCertainty.Unknown, ledger.Attempts[0].Outcome);
        Assert.Equal("outcome_not_recorded", ledger.Attempts[0].FailureCode);
        Assert.Equal(EmailChallengeDeliveryAttempt.Sent, ledger.Attempts[1].Outcome);
    }

    [Fact]
    public async Task ShouldRetryWithinTheBudgetThenFailGivenOnlyDefiniteTransientRejections()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (_, challengeId, record) = await host.IssueVerificationAsync();
        for (var count = 0; count < EmailChallengeDeliveryRecord.MaximumAttempts; count++)
            host.Relay.Then(new EmailChallengeDeliveryException(EmailChallengeFailureCertainty.DefiniteTransient,
                "smtp_transient_rejection"));

        // Act
        var process = await host.DriveAsync(record);
        for (var count = 1; count < EmailChallengeDeliveryRecord.MaximumAttempts; count++)
            process = await host.ResumeAsync(challengeId);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(EmailChallengeDeliveryRecord.MaximumAttempts, host.Relay.Sends.Count);
        Assert.Equal("failed", ledger!.Status);
        Assert.All(ledger.Attempts, attempt => Assert.Equal(EmailChallengeFailureCertainty.DefiniteTransient, attempt.Outcome));
    }

    [Fact]
    public async Task ShouldNotRetryGivenDefinitePermanentRejection()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (_, challengeId, record) = await host.IssueVerificationAsync();
        host.Relay.Then(new EmailChallengeDeliveryException(EmailChallengeFailureCertainty.DefinitePermanent,
            "smtp_permanent_rejection"));

        // Act
        var process = await host.DriveAsync(record);
        var resumed = await host.ResumeAsync(challengeId);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(host.Relay.Sends);
        Assert.Equal("failed", ledger!.Status);
    }

    [Fact]
    public async Task ShouldIgnoreAnOldCallbackGivenANewerAttempt()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var challengeId = Uuid.CreateVersion4();
        var now = host.Clock.GetUtcNow();
        var first = (await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification, now)).Latest!;
        Assert.True((await host.SendAsync(new RecordEmailChallengeDeliveryFailed(Uuid.CreateVersion4(), challengeId,
            first.AttemptId, EmailChallengeFailureCertainty.Unknown, "transport_ambiguous", now))).IsSuccess);
        var second = (await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification, now)).Latest!;
        var before = await host.Ledger.ReadAsync(challengeId);

        // Act
        var late = await host.SendAsync(new RecordEmailChallengeDeliverySent(Uuid.CreateVersion4(), challengeId,
            first.AttemptId, now));
        var after = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.True(late.IsSuccess);
        Assert.Equal(before, after);
        Assert.Equal("pending", after!.Status);
        Assert.True(after.Latest!.IsOpen);
        Assert.Equal(second.AttemptId, after.Latest.AttemptId);
        Assert.Equal(EmailChallengeFailureCertainty.Unknown, after.Attempts[0].Outcome);
    }

    [Fact]
    public async Task ShouldSettleTheLatestAttemptGivenLaterEvidenceForAnAmbiguousResult()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var challengeId = Uuid.CreateVersion4();
        var now = host.Clock.GetUtcNow();
        var attempt = (await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification, now)).Latest!;
        Assert.True((await host.SendAsync(new RecordEmailChallengeDeliveryFailed(Uuid.CreateVersion4(), challengeId,
            attempt.AttemptId, EmailChallengeFailureCertainty.Unknown, "transport_ambiguous", now))).IsSuccess);

        // Act
        var evidence = await host.SendAsync(new RecordEmailChallengeDeliverySent(Uuid.CreateVersion4(), challengeId,
            attempt.AttemptId, now));
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.True(evidence.IsSuccess);
        Assert.Equal("delivered", ledger!.Status);
    }

    [Fact]
    public async Task ShouldReplayOutcomeGivenExactRetryAndConflictGivenADifferentOutcomeForTheSameAttempt()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var challengeId = Uuid.CreateVersion4();
        var now = host.Clock.GetUtcNow();
        var attempt = (await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification, now)).Latest!;
        var failed = new RecordEmailChallengeDeliveryFailed(Uuid.CreateVersion4(), challengeId, attempt.AttemptId,
            EmailChallengeFailureCertainty.DefinitePermanent, "smtp_permanent_rejection", now);

        // Act
        var first = await host.SendAsync(failed);
        var retry = await host.SendAsync(failed);
        var contradiction = await host.SendAsync(new RecordEmailChallengeDeliverySent(Uuid.CreateVersion4(), challengeId,
            attempt.AttemptId, now));
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(contradiction.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, contradiction.Error.Kind);
        Assert.Equal("failed", ledger!.Status);
    }

    [Fact]
    public async Task ShouldRejectAnOutcomeGivenAnAttemptTheChallengeNeverOpened()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var challengeId = Uuid.CreateVersion4();
        await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification, host.Clock.GetUtcNow());

        // Act
        var result = await host.SendAsync(new RecordEmailChallengeDeliverySent(Uuid.CreateVersion4(), challengeId,
            Uuid.CreateVersion4(), host.Clock.GetUtcNow()));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectAnOutcomeGivenANonSystemActor()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var challengeId = Uuid.CreateVersion4();
        var attempt = (await host.Ledger.OpenAttemptAsync(challengeId, EmailChallengeDeliveryIntent.Verification,
            host.Clock.GetUtcNow())).Latest!;
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var result = await host.SendAsync(new RecordEmailChallengeDeliverySent(Uuid.CreateVersion4(), challengeId,
            attempt.AttemptId, host.Clock.GetUtcNow()), user);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.True((await host.Ledger.ReadAsync(challengeId))!.Latest!.IsOpen);
    }

    [Fact]
    public async Task ShouldSupersedeWithoutSendingGivenChallengeVerifiedBeforeDelivery()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueVerificationAsync();
        var token = await host.VerificationTokenAsync(owner, challengeId);
        await host.SeedAsync(new EmailAddress(EmailChallengeDeliveryHost.Email, challengeId, nameof(CompleteEmailChallenge), challengeId),
            address => address.CompleteChallenge(owner, token, host.Clock.GetUtcNow()));

        // Act
        var process = await host.DriveAsync(record);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Contains("superseded", process.Note, StringComparison.Ordinal);
        Assert.Empty(host.Relay.Sends);
        Assert.Null(await host.Ledger.ReadAsync(challengeId));
    }

    [Fact]
    public async Task ShouldSupersedeWithoutSendingGivenANewerChallengeOnTheAddress()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueVerificationAsync();
        var newerChallengeId = Uuid.CreateVersion4();
        await host.SeedAsync(new EmailAddress(EmailChallengeDeliveryHost.Email, newerChallengeId, nameof(IssueEmailChallenge), newerChallengeId),
            address => address.IssueChallenge(owner, newerChallengeId, new string('A', 64),
                host.Clock.GetUtcNow().AddMinutes(15), host.Clock.GetUtcNow()));

        // Act
        var process = await host.DriveAsync(record);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Empty(host.Relay.Sends);
        Assert.Null(await host.Ledger.ReadAsync(challengeId));
    }

    [Fact]
    public async Task ShouldFailWithoutSendingGivenATokenKeyThatIsNoLongerAvailable()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var owner = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        var now = host.Clock.GetUtcNow();
        await host.SeedAsync(new EmailAddress(EmailChallengeDeliveryHost.Email), address =>
        {
            var reserved = address.Reserve(owner);
            return reserved.IsSuccess
                ? address.IssueChallenge(owner, challengeId, new string('A', 64), now.AddMinutes(15), now, "retired-key")
                : reserved;
        });
        var record = await host.FirstRecordAsync<EmailChallengeIssued>(
            new EmailAddress(EmailChallengeDeliveryHost.Email).Stream);

        // Act
        var process = await host.DriveAsync(record);
        var ledger = await host.Ledger.ReadAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Empty(host.Relay.Sends);
        Assert.Equal("failed", ledger!.Status);
        Assert.Equal("key_unavailable", Assert.Single(ledger.Attempts).FailureCode);
    }

    [Fact]
    public async Task ShouldNeverRetainTheTokenOrTheRecipientGivenALedgerAndProcessRecord()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueVerificationAsync();
        host.Relay.Then(new TimeoutException("provider-secret for person@example.com"));
        await host.DriveAsync(record);
        await host.ResumeAsync(challengeId);
        var token = await host.VerificationTokenAsync(owner, challengeId);

        // Act
        var ledger = JsonSerializer.Serialize((await host.Ledger.ReadAsync(challengeId))!,
            ComplianceCoreJsonContext.Default.EmailChallengeDeliveryRecord);
        var process = JsonSerializer.Serialize((await host.ProcessAsync(challengeId))!,
            ComplianceCoreJsonContext.Default.ProcessRecord);
        var issued = JsonSerializer.Serialize((EmailChallengeIssued)record.Event,
            ComplianceCoreJsonContext.Default.EmailChallengeIssued);

        // Assert
        foreach (var text in new[] { ledger, process, issued })
        {
            Assert.DoesNotContain(token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(EmailChallengeDeliveryHost.Email, ledger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotSendAgainGivenARerunOfACompletedProcess()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (_, challengeId, record) = await host.IssueVerificationAsync();
        await host.DriveAsync(record);

        // Act
        var rerun = await host.DriveAsync(record);
        var resumed = await host.ResumeAsync(challengeId);

        // Assert
        Assert.Equal(ProcessState.Completed, rerun.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(host.Relay.Sends);
    }

    [Fact]
    public async Task ShouldStartTheProcessForEveryChallengeKindGivenTheRootReactorAndSkipExpiredChallenges()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (_, verificationId, verification) = await host.IssueVerificationAsync("verify@example.com");
        var (_, recoveryId, recovery) = await host.IssueRecoveryAsync("recover@example.com");
        await using var scope = host.Provider.CreateAsyncScope();
        var reactor = new EmailChallengeDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<ProcessRootReaction>(), host.Content, host.Clock);

        // Act
        await reactor.HandleAsync(new Context<EmailChallengeIssued>(verification), CancellationToken.None);
        await reactor.HandleAsync(new Context<IdentityRecoveryChallengeIssued>(recovery), CancellationToken.None);
        host.Clock.Advance(TimeSpan.FromMinutes(16));
        var (_, expiredId, expired) = await host.IssueVerificationAsync("expired@example.com");
        host.Clock.Advance(TimeSpan.FromMinutes(16));
        await reactor.HandleAsync(new Context<EmailChallengeIssued>(expired), CancellationToken.None);

        // Assert
        Assert.Equal(ProcessState.Completed, (await host.ProcessAsync(verificationId))!.State);
        Assert.Equal(ProcessState.Completed, (await host.ProcessAsync(recoveryId))!.State);
        Assert.Null(await host.ProcessAsync(expiredId));
        Assert.Equal(2, host.Relay.Accepted.Count);
    }

    [Fact]
    public async Task ShouldReportTheLedgerStatusGivenChallengeStatusRead()
    {
        // Arrange
        await using var host = new EmailChallengeDeliveryHost();
        var (owner, challengeId, record) = await host.IssueVerificationAsync();
        var handler = new GetEmailChallengeStatusHandler(host.Provider.GetRequiredService<IAggregateReader>(),
            host.Ledger, host.Clock);
        var query = new RequestContext<GetEmailChallengeStatus>(new GetEmailChallengeStatus(owner,
            EmailChallengeDeliveryHost.Email), RequestActor.System);

        // Act
        var pending = await handler.HandleAsync(query, CancellationToken.None);
        await host.DriveAsync(record);
        var delivered = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal("pending", pending.Value!.DeliveryStatus);
        Assert.Equal("delivered", delivered.Value!.DeliveryStatus);
        Assert.NotNull(await host.Ledger.ReadAsync(challengeId));
    }

    sealed class Context<TFact>(DomainEventRecord record) : IReactorContext<TFact> where TFact : DomainEvent
    {
        public TFact Trigger => (TFact)record.Event;
        public DomainEventRecord Source => record;
        public ClaimsPrincipal Actor => RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId => record.Event.Metadata.EventId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }

    /// <summary>Loses the process after the relay accepted a message: the next outcome write of a Sent attempt fails once.</summary>
    sealed class CrashOnSentLedger
    {
        bool _armed;

        public IEmailChallengeDeliveryLedger Wrap(IEmailChallengeDeliveryLedger inner) => new Decorator(inner, this);

        public void Arm() => _armed = true;

        public void Disarm() => _armed = false;

        sealed class Decorator(IEmailChallengeDeliveryLedger inner, CrashOnSentLedger owner) : IEmailChallengeDeliveryLedger
        {
            public Task<EmailChallengeDeliveryRecord?> ReadAsync(Uuid challengeId, CancellationToken ct = default) =>
                inner.ReadAsync(challengeId, ct);

            public Task<EmailChallengeDeliveryRecord> OpenAttemptAsync(Uuid challengeId, string purpose,
                DateTimeOffset now, CancellationToken ct = default) => inner.OpenAttemptAsync(challengeId, purpose, now, ct);

            public Task<EmailChallengeOutcomeDecision> RecordOutcomeAsync(Uuid challengeId, Uuid attemptId,
                string outcome, string? failureCode, Uuid commandId, DateTimeOffset at, CancellationToken ct = default)
            {
                if (owner._armed && outcome == EmailChallengeDeliveryAttempt.Sent)
                    throw new InvalidOperationException("simulated crash");
                return inner.RecordOutcomeAsync(challengeId, attemptId, outcome, failureCode, commandId, at, ct);
            }
        }
    }
}
