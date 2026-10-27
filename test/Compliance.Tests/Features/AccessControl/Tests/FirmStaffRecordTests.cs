using System.Collections;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Reactors;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class FirmStaffRecordTests
{
    static readonly Uuid Operator = Uuid.CreateVersion4();

    static ClaimsPrincipal Actor => ActorFor(Operator);

    [Fact]
    public async Task ShouldKeepBoundedStaffAuthorityGivenRepeatedStatusChanges()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()))).IsSuccess);
        for (var index = 0; index < 48; index++)
        {
            var response = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff,
                index % 2 != 0, $"Reviewed status change {index + 1}", index + 1), Actor);
            Assert.True(response.IsSuccess, response.Error?.Message);
        }

        // Act
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff));
        var retained = typeof(FirmStaffRecord).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(49UL, owner.Revision);
    }

    [Fact]
    public async Task ShouldReplayEqualStatusWinnerGivenLosingImmutableStaffWriteUnavailable()
    {
        // Arrange
        LosingStaffStore content = null!;
        await using var host = new Host(configureContent: inner => content = new LosingStaffStore(inner));
        var staff = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()))).IsSuccess);
        var command = new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false, "Reviewed inactive status", 1);
        Result<FirmStaffMemberView> winner = default;
        content.Arm(async () => winner = await host.Bus.SendAsync(command, Actor));
        Result<FirmStaffMemberView> actual = default;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await host.Bus.SendAsync(command, Actor));

        // Assert
        Assert.True(content.Failed);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        Assert.Null(failure);
        Assert.True(actual.IsSuccess, actual.Error?.Message);
        Assert.Equal(WireJson.Of(winner.Value), WireJson.Of(actual.Value));
        Assert.Equal(2UL, (await host.HydrateAsync(new FirmStaffRecord(staff))).Revision);
    }

    [Fact]
    public async Task ShouldReplayCompletedRegistrationGivenEqualWinnerBeforeLosingStaffWriteUnavailable()
    {
        // Arrange
        LosingStaffStore content = null!;
        await using var host = new Host(configureContent: inner => content = new LosingStaffStore(inner));
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(staff, user), Actor)).IsSuccess);
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff));
        var source = NativeFactReference.Of(owner.Stream, owner.Accepted!);
        var record = ProcessRecord.Start(ProcessKey.ForRoot(FirmStaffEffects.ProcessName, staff, source), source,
            owner.Accepted!.Operation.Episode, owner.Accepted.Intent, [user.ToString()], DateTimeOffset.UtcNow);
        var catalog = host.Provider.GetRequiredService<ProcessCatalog>().Get(FirmStaffEffects.ProcessName);
        var claim = (ClaimFirmStaffIdentity)(await catalog.CreateReceiver(new ProcessStep(record, "claim",
            nameof(ClaimFirmStaffIdentity), user.ToString(), FirmStaffEffects.ReceiverCommandId(source.EventId,
                nameof(ClaimFirmStaffIdentity), user.ToString(), record.Episode)), host.Provider, CancellationToken.None))!;
        Assert.True((await host.Bus.SendAsync(claim, RequestActor.System)).IsSuccess);
        var command = (CompleteFirmStaffRegistration)(await catalog.CreateReceiver(new ProcessStep(record, "complete",
            nameof(CompleteFirmStaffRegistration), null, FirmStaffEffects.ReceiverCommandId(source.EventId,
                nameof(CompleteFirmStaffRegistration), null, record.Episode)), host.Provider, CancellationToken.None))!;
        Result<ReceiverOutcome> winner = default;
        content.Arm(async () => winner = await host.Bus.SendAsync(command, RequestActor.System));
        Result<ReceiverOutcome> actual = default;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await host.Bus.SendAsync(command, RequestActor.System));

        // Assert
        Assert.True(content.Failed);
        Assert.Null(failure);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        Assert.True(actual.IsSuccess, actual.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(actual.Value).Fact);
        Assert.Equal(1UL, (await host.HydrateAsync(new FirmStaffRecord(staff))).Revision);
    }

    sealed class LosingStaffStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        Func<Task>? _admit;
        public bool Failed { get; private set; }
        public void Arm(Func<Task> admit) => _admit = admit;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && _admit is { } admit && schema == "FirmStaffChangeRecorded.StaffContent.v1")
            {
                Failed = true;
                await admit();
                throw new ImmutableContentUnavailableException("The losing staff status candidate write is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResolveRegistrationWithoutWritesGivenAcceptedRetry(bool changed)
    {
        // Arrange
        StaffContentStore content = null!;
        await using var host = new Host(configureContent: inner => content = new StaffContentStore(inner));
        var request = Register(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var first = await host.Bus.SendAsync(request, Actor);
        Assert.True(first.IsSuccess);
        var writes = content.WriteAttempts;
        content.RejectWrites = true;

        // Act
        var retry = await host.Bus.SendAsync(changed ? request with { SourceReference = "Changed source" } : request, Actor);

        // Assert
        if (changed)
            Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        else
        {
            Assert.True(retry.IsSuccess);
            Assert.Equal(first.Value, retry.Value);
        }
        Assert.Equal(writes, content.WriteAttempts);
    }

    [Fact]
    public async Task ShouldRejectRegistrationWithoutWritesGivenInvalidInitialRevision()
    {
        // Arrange
        StaffContentStore content = null!;
        await using var host = new Host(configureContent: inner => content = new StaffContentStore(inner));
        _ = host.Content;
        content.RejectWrites = true;
        var request = Register(Uuid.CreateVersion4(), Uuid.CreateVersion4()) with { ExpectedRevision = 1 };

        // Act
        var result = await host.Bus.SendAsync(request, Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, content.WriteAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldResolveRegistrationBeforeDirectoryLookupGivenAcceptedRetry(bool changed)
    {
        // Arrange
        await using var host = new Host();
        var request = Register(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var first = await host.Bus.SendAsync(request, Actor);
        Assert.True(first.IsSuccess);
        host.UserDirectoryKnown = false;

        // Act
        var retry = await host.Bus.SendAsync(changed ? request with { SourceReference = "Changed source" } : request, Actor);

        // Assert
        if (changed)
            Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        else
        {
            Assert.True(retry.IsSuccess);
            Assert.Equal(first.Value, retry.Value);
        }
    }

    [Fact]
    public async Task ShouldRejectClaimGivenWirePracticeDiffersFromFrozenRegistration()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(staff, user), Actor)).IsSuccess);
        var record = await FirmStaffFacts.RunProcessAsync(host.Provider, staff);
        var factory = FirmStaffRegistrationProcess.Definition();
        var command = Assert.IsType<ClaimFirmStaffIdentity>(await factory.CreateReceiver(new ProcessStep(record,
            "claim", nameof(ClaimFirmStaffIdentity), user.ToString(), FirmStaffEffects.ReceiverCommandId(
                record.Source.EventId, nameof(ClaimFirmStaffIdentity), user.ToString(), record.Episode)),
            host.Scope.ServiceProvider, CancellationToken.None));
        var wire = JsonSerializer.SerializeToNode(command, ComplianceCoreJsonContext.Default.ClaimFirmStaffIdentity)!;
        wire["practice"] = "advisory";
        var changed = wire.Deserialize(ComplianceCoreJsonContext.Default.ClaimFirmStaffIdentity)!;

        // Act
        var result = await host.Bus.SendAsync(changed, RequestActor.System);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.Equal(staff, (await host.HydrateAsync(new FirmStaffIdentityClaim(user))).ClaimedByStaffMemberId);
    }

    [Fact]
    public async Task ShouldKeepOneStaffRecordGivenCanonicalUserRegistrationRaceWithCrashAfterClaim()
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new Host(crash);
        var user = Uuid.CreateVersion4();
        var winner = Uuid.CreateVersion4();
        var loser = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(winner, user), Actor)).IsSuccess);
        Assert.True((await host.Bus.SendAsync(Register(loser, user), Actor)).IsSuccess);

        // Act
        crash.ArmAfterReplaceCount(1); // lease lands, the advance after the claim stage crashes
        var failure = await Record.ExceptionAsync(() => FirmStaffFacts.RunProcessAsync(host.Provider, winner));
        var claimedBeforeResume = (await host.HydrateAsync(new FirmStaffIdentityClaim(user))).ClaimedByStaffMemberId;
        var pendingBeforeResume = (await host.HydrateAsync(new FirmStaffRecord(winner))).IsPending;
        var rejected = await FirmStaffFacts.RunProcessAsync(host.Provider, loser);
        var resumed = await FirmStaffFacts.RunProcessAsync(host.Provider, winner);

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(winner, claimedBeforeResume);
        Assert.True(pendingBeforeResume);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(ProcessState.Completed, rejected.State);
        Assert.Contains("superseded", rejected.Note, StringComparison.Ordinal);
        var winnerRecord = await host.HydrateAsync(new FirmStaffRecord(winner));
        var loserRecord = await host.HydrateAsync(new FirmStaffRecord(loser));
        Assert.True(winnerRecord.IsRegistered);
        Assert.Equal(1UL, winnerRecord.Revision);
        Assert.True(loserRecord.IsRejected);
        Assert.False(loserRecord.IsRegistered);
        Assert.Single(await host.EventsAsync<FirmStaffIdentityClaimed>(new FirmStaffIdentityClaim(user).Stream));
        Assert.Empty(await host.EventsAsync<FirmStaffChangeRecorded>(loserRecord.Stream));
        Assert.Single(await host.EventsAsync<FirmStaffRegistrationRejected>(loserRecord.Stream));
        var views = host.Scope.ServiceProvider.GetRequiredService<FirmStaffViews>();
        Assert.Equal(winner, (await views.FindByUserAsync(user))!.StaffMemberId);
        Assert.Null(await views.GetAsync(loser));
    }

    [Fact]
    public async Task ShouldRegisterGivenMoreThanOneThousandStaffRecords()
    {
        // Arrange
        await using var host = new Host();
        for (var index = 0; index < 1000; index++)
            await FirmStaffFacts.RegisterAsync(host.Provider, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var staff = Uuid.CreateVersion4();

        // Act
        var admitted = await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()));
        await FirmStaffFacts.ProjectDirectoryAsync(host.Provider);
        var directory = await host.Bus.SendAsync(new GetFirmStaffDirectory(), Actor);
        var assignable = await host.Scope.ServiceProvider.GetRequiredService<IFirmStaffDirectoryReader>()
            .ListAsync(true);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.True(directory.IsSuccess, directory.Error?.Message);
        Assert.Equal(1001, directory.Value!.Staff.Count);
        Assert.Equal(1001, assignable.Staff.Count);
        Assert.Contains(directory.Value.Staff, item => item.StaffMemberId == staff && item.Revision == 1);
        Assert.Equal(directory.Value.Staff.Select(item => item.StaffMemberId.ToString()).Order(StringComparer.Ordinal),
            directory.Value.Staff.Select(item => item.StaffMemberId.ToString()));
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentAndAppendNothingGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var request = Register(staff, Uuid.CreateVersion4());
        var first = await host.Bus.SendAsync(request, Actor);
        await FirmStaffFacts.RunProcessAsync(host.Provider, staff);
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic deactivation", 1), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retry = await host.Bus.SendAsync(request, Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(staff, retry.Value!.OperationId);
        Assert.Equal($"/api/v1/platform/operations/{staff}", retry.Value.StatusUrl);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var accepted = Assert.Single(await host.EventsAsync<FirmStaffRegistrationAccepted>(stream));
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, FirmStaffContent.Realm,
            accepted.FirstResponse!)).HttpStatus);
        Assert.Equal(Operator.ToString(), accepted.Operation.Actor.Id);
    }

    [Fact]
    public async Task ShouldConflictGivenSameStaffMemberWithChangedIntentOrOperator()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(staff, user), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var otherPractice = await host.Bus.SendAsync(Register(staff, user) with { Practice = "advisory" }, Actor);
        var otherUser = await host.Bus.SendAsync(Register(staff, Uuid.CreateVersion4()), Actor);
        var otherOperator = await host.Bus.SendAsync(Register(staff, user), ActorFor(Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherPractice.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherUser.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherOperator.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMalformedRegistrationOrStaleRevision()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();

        // Act
        var empty = await host.Bus.SendAsync(Register(Uuid.Empty, user), Actor);
        var practice = await host.Bus.SendAsync(Register(staff, user) with { Practice = "audit" }, Actor);
        var revision = await host.Bus.SendAsync(Register(staff, user) with { ExpectedRevision = 1 }, Actor);
        await using var unknownHost = new Host(unknownUsers: true);
        var unknown = await unknownHost.Bus.SendAsync(Register(staff, user), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, empty.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, practice.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, revision.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Null((await host.HydrateAsync(new FirmStaffRecord(staff))).Accepted);
    }

    [Fact]
    public async Task ShouldReturnOriginalViewAndAppendNothingGivenExactStatusRetryAfterLaterChange()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()))).IsSuccess);
        var command = new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false, "Synthetic deactivation", 1);
        var first = await host.Bus.SendAsync(command, Actor);
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, true,
            "Synthetic reactivation", 2), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retry = await host.Bus.SendAsync(command, Actor);
        var changedReason = await host.Bus.SendAsync(command with { Reason = "Another reason" }, Actor);
        var changedStatus = await host.Bus.SendAsync(command with { IsActive = true }, Actor);
        var stale = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic stale", 1), Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.False(first.Value!.IsActive);
        Assert.Equal(2, first.Value.Revision);
        Assert.Equal(RequestErrorKind.Conflict, changedReason.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedStatus.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var latest = (await host.Scope.ServiceProvider.GetRequiredService<FirmStaffViews>().GetAsync(staff))!;
        Assert.True(latest.IsActive);
        Assert.Equal(3, latest.Revision);
        var firstResponse = (await host.EventsAsync<FirmStaffChangeRecorded>(stream))
            .Single(item => item.Operation.LogicalId == command.CommandId);
        Assert.Equal(200, (await FirstResponses.ReplayAsync(host.Content, FirmStaffContent.Realm,
            firstResponse.FirstResponse!)).HttpStatus);
    }

    [Fact]
    public async Task ShouldRefuseStatusChangeGivenPendingUnknownOrRejectedRegistration()
    {
        // Arrange
        await using var host = new Host();
        var pending = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(pending, Uuid.CreateVersion4()), Actor)).IsSuccess);

        // Act
        var duringPending = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), pending, false,
            "Synthetic", 0), Actor);
        var unknown = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            false, "Synthetic", 0), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duringPending.Error?.Kind);
        Assert.True(duringPending.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReplayReceiverAndRefuseNonSystemCallerGivenCompletedRegistration()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor, Register(staff, user)))
            .IsSuccess);
        var catalog = host.Provider.GetRequiredService<ProcessCatalog>().Get(FirmStaffEffects.ProcessName);
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff));
        var record = (await host.Provider.GetRequiredService<IProcessRecordStore>().ReadAsync(
            ProcessKey.ForRoot(FirmStaffEffects.ProcessName, staff,
                NativeFactReference.Of(owner.Stream, owner.Accepted!))))!;
        var claim = (await catalog.CreateReceiver(new ProcessStep(record, "claim", nameof(ClaimFirmStaffIdentity),
            user.ToString(), FirmStaffEffects.ReceiverCommandId(record.Source.EventId,
                nameof(ClaimFirmStaffIdentity), user.ToString(), record.Episode)), host.Provider, CancellationToken.None))!;
        var stream = new FirmStaffIdentityClaim(user).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var replayed = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim, RequestActor.System);
        var denied = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim, Actor);
        var changed = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim with { StaffMemberId = Uuid.CreateVersion4() },
            RequestActor.System);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False(changed.Error?.IsTransient);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRegisterOneProcessDefinitionGivenCompositionAndRebindReevaluationReactorToStaffRecords()
    {
        // Arrange
        await using var host = new Host();

        // Act
        var definition = host.Provider.GetRequiredService<ProcessCatalog>().Get(FirmStaffEffects.ProcessName);
        var reevaluation = Assert.Single(host.Provider.GetServices<WorkloadRegistration>(),
            item => item.Name == FirmStaffStatusReevaluationReactor.WorkloadName);
        var registration = Assert.Single(host.Provider.GetServices<WorkloadRegistration>(),
            item => item.Name == FirmStaffRegistrationReactor.WorkloadName);
        var pattern = EventStreamPattern.ForPattern("bdgrz", FirmStaffRecord.Area);

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(["FirmStaffRegistrationAccepted"], definition.RootSourceEvents);
        Assert.Equal(["claim", "complete"], definition.Stages.Select(stage => stage.Stage));
        Assert.Equal([["ClaimFirmStaffIdentity"], ["CompleteFirmStaffRegistration"]],
            definition.Stages.Select(stage => stage.Receivers));
        Assert.Equal(["RejectFirmStaffRegistration"], definition.Abort);
        Assert.Equal(pattern, ((Reactor)host.Provider.CreateAsyncScope().ServiceProvider
            .GetRequiredService(reevaluation.ComponentType)).Pattern);
        Assert.Equal(pattern, ((Reactor)host.Provider.CreateAsyncScope().ServiceProvider
            .GetRequiredService(registration.ComponentType)).Pattern);
    }

    static RegisterFirmStaff Register(Uuid staffMemberId, Uuid userId) =>
        new(staffMemberId, userId, "attest", "Synthetic staff source", 0);

    static ClaimsPrincipal ActorFor(Uuid userId) => new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    /// <summary>Decorates the store so one ReplaceAsync, chosen by count, fails after the receiver already ran.</summary>
    sealed class CrashOnceStore
    {
        int _remaining = int.MaxValue;

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashOnceStore owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
                inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion,
                CancellationToken ct = default)
            {
                if (owner._remaining-- == 0)
                    throw new InvalidOperationException("Worker crashed before the CAS.");
                return inner.ReplaceAsync(record, expectedVersion, ct);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore,
                int limit, CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }

    sealed class Host : IAsyncDisposable
    {
        readonly AsyncServiceScope _scope;
        readonly Users _users;

        public bool UserDirectoryKnown { get => _users.Known; set => _users.Known = value; }

        public Host(CrashOnceStore? crash = null, bool unknownUsers = false,
            Func<IImmutableContentStore, IImmutableContentStore>? configureContent = null)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<ISourceFactResolver, EventStoreSourceFactResolver>();
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            _users = new Users(!unknownUsers);
            services.AddSingleton<IPlatformUserDirectoryReader>(_users);
            if (configureContent is not null)
                services.AddSingleton<IImmutableContentStore>(provider => configureContent(
                    new FitzKvImmutableContentStore(provider.GetRequiredService<IKvClient>())));
            services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess());
            if (crash is not null)
                services.AddSingleton<IProcessRecordStore>(provider => crash.Wrap(
                    new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = Provider.CreateAsyncScope();
        }

        public ServiceProvider Provider { get; }

        public AsyncServiceScope Scope => _scope;

        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();

        public Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore Content =>
            _scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>();

        public async Task<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate) where TAggregate : Aggregate =>
            await _scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);

        public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
        {
            var found = new List<TEvent>();
            try
            {
                await foreach (var record in _scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                                   .ReadAsync(stream, 0))
                    if (record.Event is TEvent typed)
                        found.Add(typed);
            }
            catch (InvalidOperationException)
            {
                // The in-memory store reports an unwritten stream this way.
            }

            return found;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    sealed class Users(bool known) : IPlatformUserDirectoryReader
    {
        public bool Known { get; set; } = known;

        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(Known);
    }

    sealed class OperatorAccess : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId != Uuid.Empty);
    }

    sealed class StaffContentStore(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool RejectWrites { get; set; }
        public int WriteAttempts { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            WriteAttempts++;
            if (RejectWrites)
                throw new InvalidOperationException("Content writes are unavailable.");
            return inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
