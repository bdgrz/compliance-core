using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Services;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
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
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantInvitationDeliveryProcessTests
{
    const string Email = "member@example.com";

    [Fact]
    public async Task ShouldSendExactTokenOnceAndRecordDeliveredGivenInvitation()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);

        // Act
        var process = await host.DriveDeliveryAsync(invitationId);
        var record = await host.Deliveries.ReadAsync(host.TenantId, invitationId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        var send = Assert.Single(host.Mailbox.Sends);
        Assert.Equal((TenantInvitationDeliveryRecord.AttemptIdFor(invitationId), host.TenantId, Email, token),
            (send.AttemptId, send.TenantId, send.EmailAddress, send.Token));
        Assert.Equal(TenantInvitationDeliveryRecord.Delivered, record!.Status);
        var attempt = Assert.Single(record.Attempts);
        Assert.Equal(TenantInvitationDeliveryAttempt.Accepted, attempt.Result);
        Assert.Null(attempt.FailureCode);
    }

    [Fact]
    public async Task ShouldKeepBearerTokenOutOfEveryDurableRecordGivenDelivery()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);

        // Act
        var process = await host.DriveDeliveryAsync(invitationId);
        var record = await host.Deliveries.ReadAsync(host.TenantId, invitationId);
        var invited = await host.InvitedAsync(invitationId);

        // Assert
        Assert.DoesNotContain(token, JsonSerializer.Serialize(process, ComplianceCoreJsonContext.Default.ProcessRecord),
            StringComparison.Ordinal);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(record!,
            ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord), StringComparison.Ordinal);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(invited, ComplianceCoreJsonContext.Default.TenantMemberInvited),
            StringComparison.Ordinal);
        var payload = await host.Content.ReadJsonAsync<TenantInvitationDeliveryPayload>(host.TenantId.ToString(),
            process.Intent);
        Assert.Equal(invited.TokenHash, payload.TokenHash);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(payload,
            ComplianceCoreJsonContext.Default.TenantInvitationDeliveryPayload), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRecordFailureWithoutResendGivenProviderRejection()
    {
        // Arrange
        var mailbox = new RecordingInvitationDelivery { FailFirst = true };
        await using var host = new TenantInvitationHost(mailbox: mailbox);
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);

        // Act
        var process = await host.DriveDeliveryAsync(invitationId);
        var resumed = await host.ResumeDeliveryAsync(invitationId);
        var record = await host.Deliveries.ReadAsync(host.TenantId, invitationId);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Single(mailbox.Sends);
        Assert.Equal(TenantInvitationDeliveryRecord.Failed, record!.Status);
        var attempt = Assert.Single(record.Attempts);
        Assert.Equal(TenantInvitationDeliveryAttempt.Rejected, attempt.Result);
        Assert.Equal(SendTenantInvitationHandler.DeliveryFailed, attempt.FailureCode);
        Assert.DoesNotContain("provider secret", JsonSerializer.Serialize(record,
            ComplianceCoreJsonContext.Default.TenantInvitationDeliveryRecord), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAllowNewInvitationGivenPriorDeliveryDefinitelyFailed()
    {
        // Arrange
        var mailbox = new RecordingInvitationDelivery { FailFirst = true };
        await using var host = new TenantInvitationHost(mailbox: mailbox);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        var blocked = await host.InviteMemberAsync(second, Email);
        await host.DriveDeliveryAsync(first);

        // Act
        var reissued = await host.InviteMemberAsync(second, Email);
        await host.DriveDeliveryAsync(second);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, blocked.Error?.Kind);
        Assert.True(reissued.IsSuccess, reissued.Error?.Message);
        Assert.Equal(2, mailbox.Sends.Count);
        Assert.Equal(TenantInvitationDeliveryRecord.Delivered,
            (await host.Deliveries.ReadAsync(host.TenantId, second))!.Status);
        Assert.Equal(TenantInvitationDeliveryRecord.Failed,
            (await host.Deliveries.ReadAsync(host.TenantId, first))!.Status);
    }

    [Fact]
    public async Task ShouldResendSameAttemptAndTokenGivenCrashAfterSendBeforeResultIsStored()
    {
        // Arrange
        CrashingDeliveryStore? crashing = null;
        await using var host = new TenantInvitationHost(store => crashing = new CrashingDeliveryStore(store));
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var token = await host.TokenOfAsync(invitationId);
        crashing!.CrashOnResult = true;

        // Act
        var crash = await Record.ExceptionAsync(() => host.DriveDeliveryAsync(invitationId));
        var pending = await host.Deliveries.ReadAsync(host.TenantId, invitationId);
        crashing.CrashOnResult = false;
        host.Clock.Advance(ProcessDriver.LeaseDuration + TimeSpan.FromSeconds(1));
        var resumed = await host.ResumeDeliveryAsync(invitationId);
        var delivered = await host.Deliveries.ReadAsync(host.TenantId, invitationId);

        // Assert
        Assert.NotNull(crash);
        Assert.Equal(TenantInvitationDeliveryAttempt.InFlight, Assert.Single(pending!.Attempts).Result);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(2, host.Mailbox.Sends.Count);
        Assert.All(host.Mailbox.Sends, send =>
        {
            Assert.Equal(TenantInvitationDeliveryRecord.AttemptIdFor(invitationId), send.AttemptId);
            Assert.Equal(token, send.Token);
        });
        Assert.Equal(TenantInvitationDeliveryRecord.Delivered, delivered!.Status);
        Assert.Single(delivered.Attempts);
    }

    [Fact]
    public async Task ShouldNotSendAgainGivenReplayedSendStepAfterResultIsStored()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var process = await host.DriveDeliveryAsync(invitationId);
        var send = await host.CommandAsync(process, "send", TenantInvitationDeliveryReceivers.SendTenantInvitation);

        // Act
        var replay = await host.SendAsync(send!);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
        Assert.Single(host.Mailbox.Sends);
    }

    [Fact]
    public async Task ShouldRecordKeyUnavailableGivenInvitationKeyCannotReproduceToken()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var retired = Uuid.CreateVersion4();
        var legacy = Uuid.CreateVersion4();
        await host.SeedInvitationAsync(retired, TenantInvitationFacts.Member("old@example.com"), "old-private-token", "retired");
        await host.SeedInvitationAsync(legacy, TenantInvitationFacts.Member("legacy@example.com"), "legacy-token", null);

        // Act
        await host.DriveDeliveryAsync(retired);
        await host.DriveDeliveryAsync(legacy);

        // Assert
        Assert.Empty(host.Mailbox.Sends);
        foreach (var id in new[] { retired, legacy })
        {
            var record = await host.Deliveries.ReadAsync(host.TenantId, id);
            Assert.Equal(TenantInvitationDeliveryRecord.Failed, record!.Status);
            Assert.Equal(SendTenantInvitationHandler.KeyUnavailable, Assert.Single(record.Attempts).FailureCode);
        }
    }

    [Fact]
    public async Task ShouldSkipDeliveryGivenInvitationAcceptedOrExpired()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var accepted = Uuid.CreateVersion4();
        var expired = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(accepted, Email)).IsSuccess);
        Assert.True((await host.InviteMemberAsync(expired, "late@example.com")).IsSuccess);
        Assert.True((await host.AcceptAsync(accepted, Email, await host.TokenOfAsync(accepted), Uuid.CreateVersion4())).IsSuccess);

        // Act
        var first = await host.DriveDeliveryAsync(accepted);
        host.Clock.Advance(TenantInvitation.Lifetime);
        var second = await host.DriveDeliveryAsync(expired);

        // Assert
        Assert.Equal(ProcessState.Completed, first.State);
        Assert.Equal(ProcessState.Completed, second.State);
        Assert.Empty(host.Mailbox.Sends);
        Assert.Empty((await host.Deliveries.ReadAsync(host.TenantId, accepted))!.Attempts);
        Assert.Empty((await host.Deliveries.ReadAsync(host.TenantId, expired))!.Attempts);
    }

    [Fact]
    public async Task ShouldNotSendOldInvitationGivenNewerInvitationHoldsTheMailbox()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var old = Uuid.CreateVersion4();
        var current = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(old, Email)).IsSuccess);
        await using (var scope = host.Provider.CreateAsyncScope())
        {
            var mailboxes = scope.ServiceProvider.GetRequiredService<TenantInvitationMailboxes>();
            var context = new RequestDispatchContext(RequestActor.System);
            Assert.True((await mailboxes.ReleaseAsync(context, host.TenantId, Email, old, 1, "cancelled",
                CancellationToken.None)).IsSuccess);
            Assert.True((await mailboxes.AcquireAsync(context, host.TenantId, Email, current, CancellationToken.None)).IsSuccess);
        }

        // Act
        var process = await host.DriveDeliveryAsync(old);

        // Assert
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Empty(host.Mailbox.Sends);
    }

    [Fact]
    public async Task ShouldIgnoreStaleAttemptGivenOutcomeCommandOfAnotherAttempt()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var process = await host.DriveDeliveryAsync(invitationId);
        var sent = (RecordTenantInvitationDeliverySent)(await host.CommandAsync(process, "record",
            TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent))!;
        var staleFailure = new RecordTenantInvitationDeliveryFailed(Uuid.CreateVersion4(), sent.Source, sent.TenantId,
            sent.InvitationId, Uuid.CreateVersion4(), "delivery_failed", sent.SentAt);

        // Act
        var stale = await host.SendAsync(staleFailure);
        var staleSent = await host.SendAsync(sent with { AttemptId = Uuid.CreateVersion4() });
        var record = await host.Deliveries.ReadAsync(host.TenantId, invitationId);

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(stale.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(staleSent.Value);
        Assert.Equal(TenantInvitationDeliveryRecord.Delivered, record!.Status);
    }

    [Fact]
    public async Task ShouldConflictGivenFailureOutcomeForAnAttemptThatSucceeded()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var process = await host.DriveDeliveryAsync(invitationId);
        var sent = (RecordTenantInvitationDeliverySent)(await host.CommandAsync(process, "record",
            TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent))!;

        // Act
        var contradiction = await host.SendAsync(new RecordTenantInvitationDeliveryFailed(Uuid.CreateVersion4(),
            sent.Source, sent.TenantId, sent.InvitationId, sent.AttemptId, "delivery_failed", sent.SentAt));
        var replay = await host.SendAsync(sent);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, contradiction.Error?.Kind);
        Assert.False(contradiction.Error!.IsTransient);
        Assert.IsType<ReceiverOutcome.Replayed>(replay.Value);
    }

    [Fact]
    public async Task ShouldRejectOutcomeGivenProofOfAnotherInvitation()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(first, Email)).IsSuccess);
        Assert.True((await host.InviteMemberAsync(second, "second@example.com")).IsSuccess);
        var process = await host.DriveDeliveryAsync(first);
        var sent = (RecordTenantInvitationDeliverySent)(await host.CommandAsync(process, "record",
            TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent))!;

        // Act
        var result = await host.SendAsync(sent with { InvitationId = second });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(await host.Deliveries.ReadAsync(host.TenantId, second));
    }

    [Fact]
    public async Task ShouldRejectReceiverGivenCallerThatIsNotTheSystemProcess()
    {
        // Arrange
        await using var host = new TenantInvitationHost();
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var process = await host.DriveDeliveryAsync(invitationId);
        var send = await host.CommandAsync(process, "send", TenantInvitationDeliveryReceivers.SendTenantInvitation);

        // Act
        var result = await host.SendAsync(send!, TenantInvitationHost.Caller(Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldBuildOnlyTheOutcomeReceiverGivenStoredAttemptResult()
    {
        // Arrange
        var mailbox = new RecordingInvitationDelivery { FailFirst = true };
        await using var host = new TenantInvitationHost(mailbox: mailbox);
        var invitationId = Uuid.CreateVersion4();
        Assert.True((await host.InviteMemberAsync(invitationId, Email)).IsSuccess);
        var process = await host.DriveDeliveryAsync(invitationId);

        // Act
        var sent = await host.CommandAsync(process, "record", TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliverySent);
        var failed = await host.CommandAsync(process, "record", TenantInvitationDeliveryReceivers.RecordTenantInvitationDeliveryFailed);

        // Assert
        Assert.Null(sent);
        var command = Assert.IsType<RecordTenantInvitationDeliveryFailed>(failed);
        Assert.Equal(SendTenantInvitationHandler.DeliveryFailed, command.FailureCode);
    }

    [Fact]
    public void ShouldStartFromTheInvitationFactGivenProcessDefinition()
    {
        // Arrange
        var definition = TenantInvitationDeliveryProcess.Definition;

        // Act
        var stages = definition.Stages.Select(stage => stage.Stage).ToArray();

        // Assert
        Assert.Equal("TenantInvitationDeliveryProcess", definition.Name);
        Assert.Equal([nameof(TenantMemberInvited)], definition.RootSourceEvents);
        Assert.Equal(["send", "record"], stages);
        Assert.Empty(definition.Abort);
        Assert.All(definition.Stages, stage => Assert.False(stage.IsPerTarget));
    }

    sealed class CrashingDeliveryStore(ITenantInvitationDeliveryStore inner) : ITenantInvitationDeliveryStore
    {
        public bool CrashOnResult { get; set; }

        public Task<TenantInvitationDeliveryRecord?> ReadAsync(Uuid tenantId, Uuid invitationId, CancellationToken ct = default) =>
            inner.ReadAsync(tenantId, invitationId, ct);

        public Task<TenantInvitationDeliveryRecord> CreateIfAbsentAsync(TenantInvitationDeliveryRecord record,
            CancellationToken ct = default) => inner.CreateIfAbsentAsync(record, ct);

        public Task<TenantInvitationDeliveryRecord> ReplaceAsync(TenantInvitationDeliveryRecord record, long expectedVersion,
            CancellationToken ct = default)
        {
            if (CrashOnResult && record.Attempts.Any(attempt => attempt.Result != TenantInvitationDeliveryAttempt.InFlight))
                throw new IOException("The worker died after the provider accepted the message.");
            return inner.ReplaceAsync(record, expectedVersion, ct);
        }
    }
}
