using System.Security.Claims;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProfessionalDutyChangeTests
{
    const string Realm = "bdgrz";
    static readonly Uuid Operator = Uuid.CreateVersion4();

    static ClaimsPrincipal Actor => ActorFor(Operator);

    [Fact]
    public async Task ShouldRejectDesignationGivenStaffDeactivatedBeforeUseGrant()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var stale = Active(designation, user);
        Assert.True((await host.AdmitAsync(RecordOf(stale, 0))).IsSuccess);
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic deactivation", 1), Actor)).IsSuccess);

        // Act: step 1, the staff member was deactivated before the use grant.
        var firstRun = await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null,
            stale.Evidence.AssertionId);
        var owner = await host.HydrateAsync(Owner(user));
        var claimStream = new ProfessionalAssertionIdentityClaim(stale.Evidence.AssertionId).Stream;
        var key = await host.StaffUseKeyAsync(owner, stale.Evidence.AssertionId);

        // Assert
        Assert.Equal(ProcessState.Completed, firstRun.State);
        Assert.Contains("superseded", firstRun.Note, StringComparison.Ordinal);
        Assert.True(owner.IsRejected(stale.Evidence.AssertionId));
        Assert.Equal(0UL, owner.Revision);
        Assert.Empty(await host.EventsAsync<FirmProfessionalDutyDesignationRecorded>(owner.Stream));
        Assert.Empty(await host.EventsAsync<ProfessionalAssertionIdentityClaimed>(claimStream));
        Assert.True((await host.HydrateAsync(new FirmStaffRecord(staff))).Uses.Find(key)!.IsFence);

        // Act: step 2, delayed claim, apply and use are delivered after the rejection.
        var record = (await host.ProcessRecordAsync(stale.Evidence.AssertionId))!;
        var delayed = new[]
        {
            await host.SendAsync(await Step(host, record, "acquire", nameof(AuthorizeFirmStaffProfessionalDutyUse))),
            await host.SendAsync(await Step(host, record, "claim", nameof(ClaimProfessionalAssertionIdentity))),
            await host.SendAsync(await Step(host, record, "apply", nameof(ApplyProfessionalDutyChange))),
        };

        // Assert
        Assert.All(delayed, outcome => Assert.IsType<ReceiverOutcome.Superseded>(outcome.Value));
        Assert.Empty(await host.EventsAsync<ProfessionalAssertionIdentityClaimed>(claimStream));
        Assert.Empty(await host.EventsAsync<FirmProfessionalDutyDesignationRecorded>(owner.Stream));
        Assert.Empty(await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(new FirmStaffRecord(staff).Stream));

        // Act: step 3, a new valid record acquires the active staff use, then races a deactivation.
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, true,
            "Synthetic reactivation", 2), Actor)).IsSuccess);
        var valid = Active(designation, user);
        Assert.True((await host.AdmitAsync(RecordOf(valid, 0))).IsSuccess);
        var second = await ProfessionalDutyFacts.StartProcessAsync(host.Provider, user, Duty, null,
            valid.Evidence.AssertionId);
        var acquired = await host.SendAsync(await Step(host, second, "acquire",
            nameof(AuthorizeFirmStaffProfessionalDutyUse)));
        var racing = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic racing deactivation", 3), Actor);
        var secondRun = await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null,
            valid.Evidence.AssertionId);
        var afterSettle = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic deactivation after settlement", 3), Actor);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(acquired.Value);
        Assert.Equal(RequestErrorKind.Conflict, racing.Error?.Kind);
        Assert.True(racing.Error?.IsTransient);
        Assert.Equal(ProcessState.Completed, secondRun.State);
        owner = await host.HydrateAsync(Owner(user));
        Assert.Equal(1UL, owner.Revision);
        Assert.IsType<FirmProfessionalDutyDesignationRecorded>(owner.Latest);
        Assert.Single(await host.EventsAsync<ProfessionalAssertionIdentityClaimed>(
            new ProfessionalAssertionIdentityClaim(valid.Evidence.AssertionId).Stream));
        Assert.False((await host.HydrateAsync(new FirmStaffRecord(staff))).Uses.HasActiveUse);
        Assert.True(afterSettle.IsSuccess, afterSettle.Error?.Message);

        // Act: step 4, revocation does not require active staff and holds no staff use.
        var staffStream = new FirmStaffRecord(staff).Stream;
        var useFactsBefore = (await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(staffStream)).Count +
                             (await host.EventsAsync<FirmStaffProfessionalDutyUseSettled>(staffStream)).Count;
        var revocation = Revoked(designation, user, valid.Evidence.AssertionId, 2);
        Assert.True((await host.AdmitAsync(RevokeOf(revocation, 1))).IsSuccess);
        var revokeRun = await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null,
            revocation.Evidence.AssertionId);

        // Assert
        Assert.Equal(ProcessState.Completed, revokeRun.State);
        owner = await host.HydrateAsync(Owner(user));
        Assert.Equal(2UL, owner.Revision);
        Assert.IsType<FirmProfessionalDutyDesignationRevoked>(owner.Latest);
        Assert.Equal(useFactsBefore, (await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(staffStream)).Count +
                                     (await host.EventsAsync<FirmStaffProfessionalDutyUseSettled>(staffStream)).Count);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentAndAppendNothingGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        var record = RecordOf(active, 0);
        var first = await host.AdmitAsync(record);
        await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null, active.Evidence.AssertionId);
        var revocation = Revoked(designation, user, active.Evidence.AssertionId, 2);
        var revoke = RevokeOf(revocation, 1);
        var firstRevoke = await host.AdmitAsync(revoke);
        await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null, revocation.Evidence.AssertionId);
        var stream = Owner(user).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retried = await host.AdmitAsync(record);
        var retriedRevoke = await host.AdmitAsync(revoke);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retried.Value);
        Assert.Equal(firstRevoke.Value, retriedRevoke.Value);
        Assert.Equal(active.Evidence.AssertionId, retried.Value!.OperationId);
        Assert.Equal($"/api/v1/platform/operations/{active.Evidence.AssertionId}", retried.Value.StatusUrl);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var accepted = (await host.EventsAsync<ProfessionalDutyChangeAccepted>(stream))
            .Single(item => item.Operation.LogicalId == active.Evidence.AssertionId);
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, Realm, accepted.FirstResponse!)).HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictGivenSameAssertionWithChangedIntentOrOperatorOrRevision()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        Assert.True((await host.AdmitAsync(RecordOf(active, 0))).IsSuccess);
        var stream = Owner(user).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;
        var changedToken = ProfessionalDutyAssertionTestIssuer.Issue(designation, user, Duty, null, 1, "active", null,
            DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(20), active.Evidence.AssertionId);

        // Act
        var otherRevision = await host.AdmitAsync(RecordOf(active, 1));
        var otherOperator = await host.AdmitAsync(RecordOf(active, 0), ActorFor(Uuid.CreateVersion4()));
        var otherAssertion = await host.AdmitAsync(RecordOf(changedToken, 0));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherRevision.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherOperator.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherAssertion.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenWrongRouteIdentityStaleRevisionOrInactiveStaff()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        var stream = Owner(user).Stream;

        // Act
        var otherDesignation = await host.AdmitAsync(new RecordFirmProfessionalDutyDesignation(Uuid.CreateVersion4(),
            active.Evidence.AssertionId, active.Token, 0));
        var otherAssertion = await host.AdmitAsync(new RecordFirmProfessionalDutyDesignation(designation,
            Uuid.CreateVersion4(), active.Token, 0));
        var stale = await host.AdmitAsync(RecordOf(active, 3));
        var negative = await host.AdmitAsync(RecordOf(active, -1));
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff, false);
        var inactive = await host.AdmitAsync(RecordOf(active, 0));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, otherDesignation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, otherAssertion.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, inactive.Error?.Kind);
        Assert.True(inactive.Error?.IsTransient);
        Assert.Empty(await host.EventsAsync<DomainEvent>(stream));
    }

    [Fact]
    public async Task ShouldSkipStaffStagesGivenRevocationAndNotRequireActiveStaff()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var registered = await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        var seeded = await ProfessionalDutyFacts.RecordAsync(host.Provider, active, registered,
            ActorReference.ForPlatformOperator(Operator, "Synthetic operator"), DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.True(seeded.IsSuccess, seeded.Error?.Message);
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff, false);
        var revocation = Revoked(designation, user, active.Evidence.AssertionId, 2);
        Assert.True((await host.AdmitAsync(RevokeOf(revocation, 1))).IsSuccess);

        // Act
        var record = await ProfessionalDutyFacts.StartProcessAsync(host.Provider, user, Duty, null,
            revocation.Evidence.AssertionId);
        var acquire = await host.Definition.CreateReceiver(new ProcessStep(record, "acquire",
            nameof(AuthorizeFirmStaffProfessionalDutyUse), record.Targets.Single(), Uuid.CreateVersion4()),
            host.Provider, CancellationToken.None);
        var settle = await host.Definition.CreateReceiver(new ProcessStep(record, "settle",
            nameof(SettleFirmStaffProfessionalDutyUse), record.Targets.Single(), Uuid.CreateVersion4()),
            host.Provider, CancellationToken.None);
        var run = await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null,
            revocation.Evidence.AssertionId);

        // Assert
        Assert.Null(acquire);
        Assert.Null(settle);
        Assert.Equal(ProcessState.Completed, run.State);
        var owner = await host.HydrateAsync(Owner(user));
        Assert.IsType<FirmProfessionalDutyDesignationRevoked>(owner.Latest);
        var staffStream = new FirmStaffRecord(staff).Stream;
        Assert.Empty(await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(staffStream));
        Assert.Empty(await host.EventsAsync<FirmStaffProfessionalDutyUseSettled>(staffStream));
    }

    [Fact]
    public async Task ShouldReturnOriginalGrantAndSettleByExactDispositionGivenAcquireRetry()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        Assert.True((await host.AdmitAsync(RecordOf(active, 0))).IsSuccess);
        var record = await ProfessionalDutyFacts.StartProcessAsync(host.Provider, user, Duty, null,
            active.Evidence.AssertionId);
        var acquire = await Step(host, record, "acquire", nameof(AuthorizeFirmStaffProfessionalDutyUse));
        var otherId = ((AuthorizeFirmStaffProfessionalDutyUse)acquire) with { CommandId = Uuid.CreateVersion4() };

        // Act
        var first = await host.SendAsync(acquire);
        var retry = await host.SendAsync(acquire);
        var sameKeyOtherCommand = await host.SendAsync(otherId);
        var staffRecord = await host.HydrateAsync(new FirmStaffRecord(staff));
        var key = await host.StaffUseKeyAsync(await host.HydrateAsync(Owner(user)), active.Evidence.AssertionId);
        var grant = staffRecord.Uses.GrantOf(key)!;
        await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null, active.Evidence.AssertionId);
        var settle = (SettleFirmStaffProfessionalDutyUse)(await Step(host, record, "settle",
            nameof(SettleFirmStaffProfessionalDutyUse)));
        var settledAgain = await host.SendAsync(settle);
        var parentClosed = await host.SendAsync(settle with
        {
            CommandId = Uuid.CreateVersion4(),
            Disposition = SourceUseSettlementDisposition.ParentClosed,
        });
        var canceled = await host.SendAsync(settle with
        {
            CommandId = Uuid.CreateVersion4(),
            Disposition = SourceUseSettlementDisposition.Canceled,
        });

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(first.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(retry.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(sameKeyOtherCommand.Value);
        Assert.Equal(((ReceiverOutcome.Applied)first.Value!).Fact, ((ReceiverOutcome.Replayed)retry.Value!).Fact);
        Assert.Equal(((ReceiverOutcome.Applied)first.Value).Fact, ((ReceiverOutcome.Replayed)sameKeyOtherCommand.Value!).Fact);
        Assert.Equal(1UL, grant.ReservationGeneration);
        Assert.Equal(((ReceiverOutcome.Applied)first.Value).Fact, grant.GrantEvent);
        Assert.Single(await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(new FirmStaffRecord(staff).Stream));
        Assert.IsType<ReceiverOutcome.Replayed>(settledAgain.Value);
        Assert.Equal(RequestErrorKind.Conflict, parentClosed.Error?.Kind);
        Assert.False(parentClosed.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, canceled.Error?.Kind);
        var settled = Assert.Single(await host.EventsAsync<FirmStaffProfessionalDutyUseSettled>(
            new FirmStaffRecord(staff).Stream));
        var intent = await host.Content.ReadJsonAsync<SourceUseAuthorizationIntent>(Realm, settled.Intent);
        Assert.Equal(SourceUseSettlementDisposition.IrreversibleDecision, intent.Disposition);
        Assert.Equal(grant, intent.Grant);
        Assert.Equal(SourceUsePurpose.ProfessionalDutyRecording, intent.Requirement.Purpose);
    }

    [Fact]
    public async Task ShouldFenceLateAcquisitionGivenCanceledSettlementBeforeAnyGrant()
    {
        // Arrange
        await using var host = new Host();
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var active = Active(designation, user);
        Assert.True((await host.AdmitAsync(RecordOf(active, 0))).IsSuccess);
        var record = await ProfessionalDutyFacts.StartProcessAsync(host.Provider, user, Duty, null,
            active.Evidence.AssertionId);
        var rejection = await host.SendAsync(await Step(host, record, "abort", nameof(RejectProfessionalDutyChange)));

        // Act
        var canceled = await host.SendAsync(await Step(host, record with { State = ProcessState.Aborting }, "settle",
            nameof(SettleFirmStaffProfessionalDutyUse)));
        var late = await host.SendAsync(await Step(host, record, "acquire", nameof(AuthorizeFirmStaffProfessionalDutyUse)));

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(rejection.Value);
        Assert.IsType<ReceiverOutcome.Applied>(canceled.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(late.Value);
        var staffRecord = await host.HydrateAsync(new FirmStaffRecord(staff));
        Assert.False(staffRecord.Uses.HasActiveUse);
        Assert.Equal(0UL, staffRecord.Uses.Generation);
        Assert.Empty(await host.EventsAsync<FirmStaffProfessionalDutyUseAuthorized>(staffRecord.Stream));
    }

    [Fact]
    public async Task ShouldRejectPendingChangeAndFenceLateCompletionGivenAssertionClaimedByAnotherChange()
    {
        // Arrange
        await using var host = new Host();
        var (userA, designationA) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var (userB, staffB, designationB) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staffB, userB);
        var assertionId = Uuid.CreateVersion4();
        var second = Active(designationB, userB, assertionId: assertionId);
        Assert.True((await host.AdmitAsync(RecordOf(second, 0))).IsSuccess);
        var secondRecord = await ProfessionalDutyFacts.StartProcessAsync(host.Provider, userB, Duty, null, assertionId);
        Assert.IsType<ReceiverOutcome.Applied>((await host.SendAsync(await Step(host, secondRecord, "acquire",
            nameof(AuthorizeFirmStaffProfessionalDutyUse)))).Value);
        // The identity is claimed by another change before this one reaches its claim step.
        var claimStream = new ProfessionalAssertionIdentityClaim(assertionId).Stream;
        await host.SeedForeignClaimAsync(assertionId, userA, designationA);

        // Act
        var claim = await host.SendAsync(await Step(host, secondRecord, "claim", nameof(ClaimProfessionalAssertionIdentity)));
        var lateApply = await host.SendAsync(await Step(host, secondRecord, "apply", nameof(ApplyProfessionalDutyChange)));

        // Assert
        Assert.IsType<ReceiverOutcome.Superseded>(claim.Value);
        Assert.IsType<ReceiverOutcome.Superseded>(lateApply.Value);
        var owner = await host.HydrateAsync(Owner(userB));
        Assert.True(owner.IsRejected(assertionId));
        Assert.Empty(await host.EventsAsync<FirmProfessionalDutyDesignationRecorded>(owner.Stream));
        Assert.Single(await host.EventsAsync<ProfessionalAssertionIdentityClaimed>(claimStream));
        var staffRecord = await host.HydrateAsync(new FirmStaffRecord(staffB));
        Assert.False(staffRecord.Uses.HasActiveUse);
        Assert.Single(await host.EventsAsync<FirmStaffProfessionalDutyUseSettled>(staffRecord.Stream));
    }

    [Fact]
    public async Task ShouldNotConflictGivenUnrelatedDesignationsAndConflictGivenSecondActiveDesignationForOneTuple()
    {
        // Arrange
        await using var host = new Host();
        var (userA, staffA) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var (userB, staffB) = (Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staffA, userA);
        await FirmStaffFacts.RegisterAsync(host.Provider, staffB, userB);
        var a = Active(Uuid.CreateVersion4(), userA);
        var b = Active(Uuid.CreateVersion4(), userB);

        // Act
        var admittedA = await host.AdmitAsync(RecordOf(a, 0));
        var admittedB = await host.AdmitAsync(RecordOf(b, 0));
        await ProfessionalDutyFacts.RunProcessAsync(host.Provider, userA, Duty, null, a.Evidence.AssertionId);
        await ProfessionalDutyFacts.RunProcessAsync(host.Provider, userB, Duty, null, b.Evidence.AssertionId);
        var duplicate = await host.AdmitAsync(RecordOf(Active(Uuid.CreateVersion4(), userA), 1));

        // Assert
        Assert.True(admittedA.IsSuccess, admittedA.Error?.Message);
        Assert.True(admittedB.IsSuccess, admittedB.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error?.Kind);
        Assert.Equal(1UL, (await host.HydrateAsync(Owner(userA))).Revision);
        Assert.Equal(1UL, (await host.HydrateAsync(Owner(userB))).Revision);
    }

    [Fact]
    public async Task ShouldApplyAtFrozenDecisionInstantGivenSignedValidityExpiresBeforeApply()
    {
        // Arrange
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        await using var host = new Host(clock: clock);
        var (user, staff, designation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        await FirmStaffFacts.RegisterAsync(host.Provider, staff, user);
        var issuedAt = clock.GetUtcNow().AddMinutes(-1);
        var brief = ProfessionalDutyAssertionTestIssuer.Issue(designation, user, Duty, null, 1, "active", null, issuedAt,
            issuedAt.AddMinutes(10));
        Assert.True((await host.AdmitAsync(RecordOf(brief, 0))).IsSuccess);

        // Act
        clock.UtcNow = issuedAt.AddMinutes(30);
        var run = await ProfessionalDutyFacts.RunProcessAsync(host.Provider, user, Duty, null,
            brief.Evidence.AssertionId);
        var owner = await host.HydrateAsync(Owner(user));
        var view = await host.Content.ReadJsonAsync<FirmProfessionalDutyDesignationView>(Realm, owner.LatestContent!);
        var current = await host.Scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>()
            .ReadCurrentAsync(user, Duty, null, CancellationToken.None);

        // Assert
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.True(view.IsActive);
        Assert.Equal(1, view.ContinuityStartRevision);
        Assert.Null(current);
    }

    [Fact]
    public void ShouldIssueMonotonicGenerationAndHoldUntilSettledGivenSourceUseReservations()
    {
        // Arrange
        var owner = new FirmStaffRecord(Uuid.CreateVersion4()).Stream;
        var uses = new SourceUseReservations(owner);
        var first = new ReservationKey(Uuid.CreateVersion4(), owner, 1, owner, "professional_duty_recording");
        var second = first with { RootOperationId = Uuid.CreateVersion4() };
        var third = first with { RootOperationId = Uuid.CreateVersion4() };

        // Act
        uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording, Granted(owner, first));
        uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording, Granted(owner, second));
        uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording, Settled(owner, first));
        var heldAfterOne = uses.HasActiveUse;
        uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording, Settled(owner, second));
        uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording, Settled(owner, third));

        // Assert
        Assert.Equal(1UL, uses.Find(first)!.ReservationGeneration);
        Assert.Equal(2UL, uses.Find(second)!.ReservationGeneration);
        Assert.Equal(2UL, uses.Generation);
        Assert.True(heldAfterOne);
        Assert.False(uses.HasActiveUse);
        Assert.True(uses.Find(third)!.IsFence);
        Assert.Throws<InvalidOperationException>(() => uses.ApplyGranted(SourceUsePurpose.ProfessionalDutyRecording,
            Granted(owner, third)));
        Assert.Throws<InvalidOperationException>(() => uses.ApplySettled(SourceUsePurpose.ProfessionalDutyRecording,
            Settled(owner, first)));
        Assert.Equal("professional_duty_recording", SourceUseReservations.PurposeName(SourceUsePurpose.ProfessionalDutyRecording));
    }

    [Fact]
    public async Task ShouldRegisterOneProcessDefinitionEqualToCatalogGivenComposition()
    {
        // Arrange
        await using var host = new Host();

        // Act
        var definition = host.Definition;
        var registration = Assert.Single(host.Provider.GetServices<WorkloadRegistration>(),
            item => item.Name == ProfessionalDutyChangeReactor.WorkloadName);
        var pattern = EventStreamPattern.ForPattern(Realm, FirmProfessionalDutyDesignation.Area);

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(["ProfessionalDutyChangeAccepted"], definition.RootSourceEvents);
        Assert.Equal(["acquire", "claim", "apply", "settle"], definition.Stages.Select(stage => stage.Stage));
        Assert.Equal([["AuthorizeFirmStaffProfessionalDutyUse"], ["ClaimProfessionalAssertionIdentity"],
            ["ApplyProfessionalDutyChange"], ["SettleFirmStaffProfessionalDutyUse"]],
            definition.Stages.Select(stage => stage.Receivers));
        Assert.Equal(["RejectProfessionalDutyChange"], definition.Abort);
        Assert.Equal(pattern, ((Reactor)host.Provider.CreateAsyncScope().ServiceProvider
            .GetRequiredService(registration.ComponentType)).Pattern);
    }

    const string Duty = FirmProfessionalDuty.RuleRatifier;

    static FirmProfessionalDutyDesignation Owner(Uuid user) => new(user, Duty, null);

    static async Task<IRequest<ReceiverOutcome>> Step(Host host, ProcessRecord record, string stage, string receiver)
    {
        var perTarget = stage is "acquire" or "claim" or "settle";
        var target = perTarget ? record.Targets.Single() : null;
        return (await host.Definition.CreateReceiver(new ProcessStep(record, stage, receiver, target,
            ProfessionalDutyEffects.ReceiverCommandId(record.Source.EventId, receiver, target, record.Episode)),
            host.Provider, CancellationToken.None))!;
    }

    static ProfessionalDutyAssertionTestIssuer.SignedAssertion Active(Uuid designation, Uuid user, long revision = 1,
        Uuid? supersedes = null, Uuid? assertionId = null) => ProfessionalDutyAssertionTestIssuer.Issue(designation,
        user, Duty, null, revision, "active", supersedes, DateTimeOffset.UtcNow.AddMinutes(-1),
        DateTimeOffset.UtcNow.AddMinutes(30), assertionId);

    static ProfessionalDutyAssertionTestIssuer.SignedAssertion Revoked(Uuid designation, Uuid user, Uuid supersedes,
        long revision) => ProfessionalDutyAssertionTestIssuer.Issue(designation, user, Duty, null, revision, "revoked",
        supersedes, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));

    static RecordFirmProfessionalDutyDesignation RecordOf(ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion,
        long expectedRevision) => new(assertion.Evidence.DesignationId, assertion.Evidence.AssertionId, assertion.Token,
        expectedRevision);

    static RevokeFirmProfessionalDutyDesignation RevokeOf(ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion,
        long expectedRevision) => new(assertion.Evidence.DesignationId, assertion.Evidence.AssertionId, assertion.Token,
        expectedRevision);

    static FirmStaffProfessionalDutyUseAuthorized Granted(EventStreamAddress owner, ReservationKey key) =>
        new(owner, 1, (long)key.SourceGeneration, UseIntent(), null, UseProvenance(owner, key), null, null);

    static FirmStaffProfessionalDutyUseSettled Settled(EventStreamAddress owner, ReservationKey key) =>
        new(owner, 1, (long)key.SourceGeneration, UseIntent(), null, UseProvenance(owner, key), null, null);

    static ImmutableContentRef UseIntent() => new(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json",
        SourceUseAuthorizationIntent.Schema);

    static OperationProvenance UseProvenance(EventStreamAddress owner, ReservationKey key) => new(
        Uuid.CreateVersion4(), key.RootOperationId, null, "Synthetic",
        ActorReference.ForPlatformOperator(Operator, "Synthetic operator"), owner, "initial", 0, new string('b', 64),
        DateTimeOffset.UtcNow, owner, owner);

    static ClaimsPrincipal ActorFor(Uuid userId) => new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    sealed class Host : IAsyncDisposable
    {
        public Host(TimeProvider? clock = null)
        {
            var services = new ServiceCollection();
            var values = new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            };
            ProfessionalDutyAssertionTestIssuer.AddConfiguration(values);
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPlatformUserDirectoryReader>(new Users());
            services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess());
            if (clock is not null)
                services.AddSingleton(clock);
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Scope = Provider.CreateAsyncScope();
        }

        public ServiceProvider Provider { get; }

        public AsyncServiceScope Scope { get; }

        public IRequestBus Bus => Scope.ServiceProvider.GetRequiredService<IRequestBus>();

        public IImmutableContentStore Content => Scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();

        public ProcessDefinition Definition => Provider.GetRequiredService<ProcessCatalog>()
            .Get(ProfessionalDutyEffects.ProcessName);

        public ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRequest>(TRequest request,
            ClaimsPrincipal? actor = null) where TRequest : IRequest<OperationAcceptedResponse>,
            IProfessionalDutyAdministrationRequest => Bus.DispatchAsync(request, new RequestDispatchContext(actor ?? Actor,
            new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic")), CancellationToken.None);

        public async ValueTask<Result<ReceiverOutcome>> SendAsync(IRequest<ReceiverOutcome> command) =>
            await Bus.DispatchAsync(command, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        public async Task<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate) where TAggregate : Aggregate =>
            await Scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);

        public Task<ProcessRecord?> ProcessRecordAsync(Uuid assertionId) =>
            Scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().ReadAsync(
                new ProcessKey(ProfessionalDutyEffects.ProcessName, Realm, assertionId));

        public async Task<ReservationKey> StaffUseKeyAsync(FirmProfessionalDutyDesignation owner, Uuid assertionId)
        {
            var accepted = owner.AcceptedFor(assertionId)!;
            var intent = await Content.ReadJsonAsync<ProfessionalDutyChangeIntent>(Realm, accepted.Intent);
            return ProfessionalDutyRules.StaffUseKey(assertionId, owner.Stream, intent.StaffEligibility!);
        }

        /// <summary>Claims the assertion identity for another change, as a competing change that won the race.</summary>
        public async Task SeedForeignClaimAsync(Uuid assertionId, Uuid user, Uuid designation)
        {
            var executor = Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            var stream = new ProfessionalAssertionIdentityClaim(assertionId).Stream;
            var intent = await Content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), "foreign-claim-intent.v1",
                new MutationResultContent("foreign", new StandardResponseBody(new ImmutableContentRef(
                    Uuid.CreateVersion4(), new string('c', 64), 2, "application/json", "foreign"))));
            var provenance = new OperationProvenance(Uuid.CreateVersion4(), assertionId, null,
                nameof(ClaimProfessionalAssertionIdentity), ActorReference.ForPlatformOperator(Operator, "Synthetic"),
                stream, "initial", 0, new string('d', 64), DateTimeOffset.UtcNow, stream, stream);
            var claimed = await executor.ExecuteAsync(new ProfessionalAssertionIdentityClaim(assertionId),
                claim => AggregateOutcome.Commit(claim.Claim(provenance, intent)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        }

        public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
        {
            var found = new List<TEvent>();
            try
            {
                await foreach (var record in Scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
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
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    sealed class Users : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class OperatorAccess : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId != Uuid.Empty);
    }
}
