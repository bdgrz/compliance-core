using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkAssignmentNativeTests
{
    [Theory]
    [InlineData("assign")]
    [InlineData("claim")]
    [InlineData("delegate")]
    [InlineData("escalate")]
    public async Task ShouldReplayOriginalNativeResultGivenLaterAccountabilityChange(string kind)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.AddToTeamAsync(fixture.BackupUserId);
        await fixture.PlanAsync(owner: kind == "claim" ? new OperatingHolder("team", fixture.TeamId) : fixture.OwnerHolder,
            backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId,
            kind == "claim" ? "unassigned" : "mine"))).Items.First(value => !value.Escalated);
        var id = Uuid.CreateVersion4();
        IRequest<WorkItemDetailView> command = kind switch
        {
            "claim" => new ClaimWorkItem(id, fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0),
            "delegate" => new DelegateWorkItem(id, fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId, "Delegate accountability."),
            "escalate" => new EscalateWorkItem(id, fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, "Needs management attention."),
            _ => new AssignWorkItem(id, fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId, "Assign accountability."),
        };
        var actor = kind == "assign" ? fixture.LeadUserId : fixture.OwnerUserId;
        var original = await fixture.AsAsync(actor, command);
        var next = kind is "assign" or "delegate" ? fixture.OwnerMemberId : fixture.BackupMemberId;
        await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, next, "Later accountability."));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var before = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId));

        // Act
        var replay = await fixture.AsAsync(actor, command);
        var after = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId));

        // Assert
        Assert.Equal(original.Item, replay.Item);
        Assert.Equal(original.History, replay.History);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(2, after.Revision);
        Assert.NotNull(before.Latest!.OperationResult);
    }

    [Fact]
    public async Task ShouldRequireCurrentManagerAuthorityGivenOriginalAssignmentRetry()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId);
        _ = await fixture.AsAsync(fixture.LeadUserId, command);
        fixture.Permissions.Managers.Remove(fixture.LeadMemberId);
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var refused = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command, ProgramManagementServices.Actor(fixture.LeadUserId));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error?.Kind);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        Assert.Equal(1, owner.Revision);
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("actor")]
    [InlineData("revision")]
    public async Task ShouldRejectChangedAuthoredTermsGivenAcceptedCommandIdentity(string mutation)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId, "Original");
        _ = await fixture.AsAsync(fixture.LeadUserId, command);
        var changed = mutation == "intent" ? command with { Reason = "Changed" } : mutation == "revision" ? command with { ExpectedRevision = 1 } : command;
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var refused = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(changed,
            ProgramManagementServices.Actor(mutation == "actor" ? fixture.ApproverUserId : fixture.LeadUserId));

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId))).Revision);
    }
    [Fact]
    public async Task ShouldRecoverActualEqualWinnerGivenLosingChangeWriteUnavailable()
    {
        // Arrange
        FaultingContent content = null!;
        var fixture = await OperationsFixture.CreateAsync(configureContent: inner => content = new FaultingContent(inner));
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId);
        WorkItemDetailView? original = null;
        content.Arm(async () => original = await fixture.AsAsync(fixture.LeadUserId, command));
        WorkItemDetailView? actual = null;

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await fixture.AsAsync(fixture.LeadUserId, command));

        // Assert
        Assert.True(content.Failed);
        Assert.NotNull(original);
        Assert.Null(failure);
        Assert.Equal(original!.Item, actual!.Item);
        Assert.Equal(original.History, actual.History);
        await using var scope = fixture.Provider.CreateAsyncScope();
        Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(
            new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId))).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayEarlierResultGivenUnrelatedSuccessorChangeBodyUnavailableOrCorrupt(bool corrupt)
    {
        // Arrange
        FaultingContent content = null!;
        var fixture = await OperationsFixture.CreateAsync(configureContent: inner => content = new FaultingContent(inner));
        await fixture.PlanAsync(backup: new OperatingHolder("member", fixture.BackupMemberId));
        var item = (await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId, fixture.ProgramId))).Items[0];
        var command = new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.BackupMemberId);
        var original = await fixture.AsAsync(fixture.LeadUserId, command);
        await fixture.AsAsync(fixture.LeadUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.OwnerMemberId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        content.Blocked = owner.Latest!.Change;
        content.Corrupt = corrupt;

        // Act
        var replay = await fixture.AsAsync(fixture.LeadUserId, command);

        // Assert
        Assert.Equal(original.Item, replay.Item);
        Assert.Equal(original.History, replay.History);
        Assert.Equal(0, content.BlockedReads);
        await using var queryScope = fixture.Provider.CreateAsyncScope();
        var unreadable = await queryScope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId), ProgramManagementServices.Actor(fixture.LeadUserId));
        Assert.False(unreadable.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, unreadable.Error?.Kind);
        Assert.Equal(!corrupt, unreadable.Error!.IsTransient);
    }

    [Theory]
    [InlineData(OperatingAuthority.RiskApproverHolder)]
    [InlineData(OperatingAuthority.RiskExecutiveHolder)]
    public async Task ShouldRefuseRestrictedReceiptVisibilityGivenManagerNoLongerHoldsRiskDecisionDuty(string holderKind)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var candidate = new WorkCandidate(Uuid.CreateVersion4(), "risk_acceptance", Uuid.CreateVersion4(), null, null,
            "Original risk acceptance work", "Original scoped source", null, null, "accept", "/synthetic/risk",
            new OperatingHolder(holderKind, fixture.ProgramId), null, new HashSet<Uuid>(), DateTimeOffset.UtcNow)
        {
            RequiredManagementProgramId = fixture.ProgramId,
        };

        var currentAuthority = new OperatingAuthority(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            new PermissionBackedAccessGrantPermissionAuthorizer(new ProgramOnlyPermissions()));
        var queue = new WorkQueueReader(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<WorkAssignmentReader>(), scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Controls.ControlReader>(),
            scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Policies.PolicyReader>(), currentAuthority, TimeProvider.System,
            new WorkQueueReadConsistency(scope.ServiceProvider.GetRequiredService<IDomainEventReader>()));

        // Act
        var refused = await queue.AuthorizeAssignmentReceiptAsync(
            fixture.TenantId, fixture.ProgramId, new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead"),
            candidate, nameof(AssignWorkItem), CancellationToken.None);

        // Assert
        Assert.Contains(fixture.LeadMemberId, fixture.Permissions.Managers);
        Assert.False(refused.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error?.Kind);
    }

    sealed class ProgramOnlyPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission is Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission or
                Bdgrz.Compliance.Features.Programs.IProgramScopedRequest.ManagementPermission);
    }

    sealed class FaultingContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        Func<Task>? _winner;
        public bool Failed { get; private set; }
        public Bdgrz.Compliance.Features.Proofs.ImmutableContentRef? Blocked { get; set; }
        public bool Corrupt { get; set; }
        public int BlockedReads { get; private set; }
        public void Arm(Func<Task> winner) => _winner = winner;
        public async Task<Bdgrz.Compliance.Features.Proofs.ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id,
            string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && _winner is { } winner && schema == WorkAssignmentChangeContent.Schema)
            {
                Failed = true;
                await winner();
                throw new ImmutableContentUnavailableException("The losing accountability candidate is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, Bdgrz.Compliance.Features.Proofs.ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference == Blocked)
            {
                BlockedReads++;
                throw Corrupt ? new ImmutableContentIntegrityException("The unrelated successor is corrupt.") :
                    new ImmutableContentUnavailableException("The unrelated successor is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

}
