using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DirectoryIndependenceReevaluationTests
{
    [Fact]
    public async Task ShouldRetainDenialOnlyReceiptGivenRegisteredStatusReactionAndAcceptedAssignment()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 2);
        await fixture.RegisterTenantAsync();
        await fixture.ProjectAsync();
        var before = await fixture.CheckpointAsync("FirmStaffStatusReevaluationV1");
        var changed = await fixture.SetStatusAsync(fixture.Lead, false);

        // Act
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        var receipt = Assert.Single(await fixture.ReceiptsAsync());
        var view = await fixture.Services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<DirectoryIndependenceReevaluationView>(fixture.Tenant.ToString(), receipt.ReevaluationContent);
        var accepted = Assert.Single(await fixture.Services.GetRequiredService<ServiceEngagementViews>()
            .AcceptedAsync(fixture.Tenant));
        var other = fixture.Staff[1];

        // Assert
        Assert.Equal("review_required", view.State);
        Assert.True(view.ProductionAcceptanceBlocked);
        Assert.Equal(fixture.Lead.StaffMemberId, view.Source.StaffMemberId);
        Assert.Equal(changed.Revision, view.Source.StaffRevision);
        Assert.Equal(accepted.Acceptance.Revision, view.OriginalAcceptance.Revision);
        Assert.Equal("system_process", view.Actor.Kind);
        Assert.Equal(3UL, receipt.Revision);
        Assert.NotEqual(before, await fixture.CheckpointAsync("FirmStaffStatusReevaluationV1"));
        var json = IndependenceHost.Json(view);
        Assert.DoesNotContain("operator status reason", json);
        Assert.DoesNotContain("Private synthetic status operator", json);
        var effectiveAt = receipt.Operation.AcceptedAt;
        Assert.False(EngagementProfessionalAccess.IsEligible(accepted, [], fixture.Lead.StaffMemberId,
            fixture.Lead.UserId, accepted.Acceptance.Rules.Version, fixture.Lead.Revision, effectiveAt));
        Assert.True(EngagementProfessionalAccess.IsEligible(accepted, [], other.StaffMemberId, other.UserId,
            accepted.Acceptance.Rules.Version, other.Revision, effectiveAt));
    }

    [Fact]
    public async Task ShouldRetainStatusCheckpointGivenLocatorLagThenRecordReceiptOnceCovered()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        await fixture.RegisterTenantAsync();
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        var before = await fixture.CheckpointAsync("FirmStaffStatusReevaluationV1");
        await fixture.SetStatusAsync(fixture.Lead, false);

        // Act
        var pending = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            fixture.RunReactorAsync("FirmStaffStatusReevaluationV1"));
        var heldBack = await fixture.CheckpointAsync("FirmStaffStatusReevaluationV1");
        var receiptsWhilePending = await fixture.ReceiptsAsync();
        await fixture.ProjectAsync();
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        var receipt = Assert.Single(await fixture.ReceiptsAsync());
        var view = await fixture.Services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<DirectoryIndependenceReevaluationView>(fixture.Tenant.ToString(), receipt.ReevaluationContent);

        // Assert
        Assert.True(pending.Error.IsTransient);
        Assert.Equal(before, heldBack);
        Assert.Empty(receiptsWhilePending);
        Assert.Equal(fixture.Lead.StaffMemberId, view.Source.StaffMemberId);
        Assert.NotEqual(before, await fixture.CheckpointAsync("FirmStaffStatusReevaluationV1"));
    }

    [Fact]
    public async Task ShouldRecordOneReceiptGivenAcceptanceReconciledAfterEarlierStatusAndStatusReactionRerun()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 1, engagementCount: 0);
        await fixture.RegisterTenantAsync();
        var original = fixture.Lead;
        await fixture.SetStatusAsync(original, false);
        await fixture.AcceptAsync(fixture.Tenant, [original]);

        // Act
        await fixture.RunReactorAsync("AcceptedStaffDirectoryReconciliationV1");
        var first = Assert.Single(await fixture.ReceiptsAsync());
        await fixture.ProjectAsync();
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        await fixture.RunReactorAsync("AcceptedStaffDirectoryReconciliationV1");
        var receipts = await fixture.ReceiptsAsync();

        // Assert
        var retained = Assert.Single(receipts);
        Assert.Equal(first.Operation.LogicalId, retained.Operation.LogicalId);
        Assert.Equal(first.ReevaluationContent, retained.ReevaluationContent);
    }

    [Fact]
    public async Task ShouldReconcileEveryClientOnceGivenStatusChangeForStaffAssignedInTwoClients()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        var other = Uuid.CreateVersion4();
        await fixture.AcceptAsync(other, fixture.Staff);
        await fixture.RegisterTenantAsync();
        await fixture.RegisterTenantAsync(other);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(other);
        await fixture.SetStatusAsync(fixture.Lead, false);

        // Act
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");

        // Assert
        Assert.Single(await fixture.ReceiptsAsync());
        var foreign = Assert.Single(await fixture.ReceiptsAsync(other));
        Assert.Equal(other, foreign.TenantId);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("client")]
    [InlineData("generic_system")]
    [InlineData("wrong_process")]
    [InlineData("mcp")]
    [InlineData("http")]
    [InlineData("causation")]
    [InlineData("directory_offset")]
    [InlineData("directory_event")]
    [InlineData("directory_digest")]
    [InlineData("acceptance_event")]
    [InlineData("acceptance_request")]
    [InlineData("acceptance_digest")]
    [InlineData("foreign_tenant")]
    [InlineData("foreign_user")]
    [InlineData("foreign_engagement")]
    [InlineData("other_epoch")]
    [InlineData("foreign_command")]
    public async Task ShouldRefuseSourceEffectWithoutAppendGivenUntrustedActorTransportOrSource(string change)
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync();
        await fixture.RegisterTenantAsync();
        await fixture.SetStatusAsync(fixture.Lead, false);
        var command = await CommandAsync(fixture);
        var actor = change switch
        {
            "anonymous" => RequestActor.Anonymous,
            "client" => IndependenceHost.ActorFor(Uuid.CreateVersion4()),
            "generic_system" => RequestActor.System,
            "wrong_process" => RequestActor.CreateSystem("reactor:OtherProcess", "bdgrz.system"),
            _ => RequestActor.CreateSystem("reactor:FirmStaffStatusReevaluationV1", "bdgrz.system"),
        };
        RequestInvocation invocation = change switch
        {
            "mcp" => new McpInvocation("synthetic-directory-effect"),
            "http" => new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"),
            _ => new DirectInvocation(),
        };
        var altered = change switch
        {
            "directory_offset" => command with { DirectoryResourceOffset = 0 },
            "directory_event" => command with { DirectoryEventId = Uuid.CreateVersion4() },
            "directory_digest" => command with { DirectoryPayloadSha256 = new string('0', 64) },
            "acceptance_event" => command with { AcceptanceEventId = Uuid.CreateVersion4() },
            "acceptance_request" => command with { AcceptanceRequestId = Uuid.CreateVersion4() },
            "acceptance_digest" => command with { AcceptancePayloadSha256 = new string('0', 64) },
            "foreign_tenant" => command with { TenantId = Uuid.CreateVersion4() },
            "foreign_user" => command with { UserId = Uuid.CreateVersion4() },
            "foreign_engagement" => command with { EngagementId = Uuid.CreateVersion4() },
            "other_epoch" => command with { AcceptedServiceEpoch = 7 },
            "foreign_command" => command with { CommandId = Uuid.CreateVersion4() },
            _ => command,
        };
        if (change is not ("foreign_command" or "anonymous" or "client" or "generic_system" or "wrong_process" or "mcp" or
            "http" or "causation"))
            altered = altered with
            {
                CommandId = RecordDirectoryIndependenceReevaluation.CommandIdOf(altered.DirectoryEventId,
                    altered.AcceptanceEventId, altered.StaffMemberId, altered.UserId),
            };
        var context = new RequestDispatchContext(actor, invocation, new RequestMetadata(altered.CommandId,
            Uuid.CreateVersion4(), change == "causation" ? Uuid.CreateVersion4() : command.DirectoryEventId));
        var before = (await fixture.SourceRecordsAsync()).Count;

        // Act
        var result = await fixture.Services.GetRequiredService<IRequestBus>().DispatchAsync(altered, context);

        // Assert
        Assert.False(result.IsSuccess);
        if (change is "anonymous" or "client" or "generic_system" or "wrong_process" or "mcp" or "http")
            Assert.True(result.Error!.Kind is RequestErrorKind.Forbidden or RequestErrorKind.Unauthorized,
                result.Error.Message);
        else
        {
            Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
            Assert.Contains("exact authoritative cause", result.Error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(before, (await fixture.SourceRecordsAsync()).Count);
        Assert.Empty(await fixture.ReceiptsAsync());
        Assert.False(typeof(ICallable).IsAssignableFrom(typeof(RecordDirectoryIndependenceReevaluation)));
        Assert.False(typeof(IQueuable).IsAssignableFrom(typeof(RecordDirectoryIndependenceReevaluation)));
    }

    [Fact]
    public async Task ShouldReplayCommittedReceiptWithoutAppendGivenExactRedeliveryAfterEngagementClosure()
    {
        // Arrange
        await using var fixture = await AcceptedStaffFixture.CreateAsync(staffCount: 2);
        await fixture.RegisterTenantAsync();
        await fixture.ProjectAsync();
        await fixture.SetStatusAsync(fixture.Lead, false);
        await fixture.RunReactorAsync("FirmStaffStatusReevaluationV1");
        var command = await CommandAsync(fixture);
        var first = Assert.Single(await fixture.ReceiptsAsync());
        var accepted = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var engagement = Uuid.Parse(accepted.Stream.Resource, CultureInfo.InvariantCulture);
        var view = Assert.Single(await fixture.Services.GetRequiredService<ServiceEngagementViews>()
            .AcceptedAsync(fixture.Tenant)).Acceptance;
        await ServiceEngagementFacts.CloseAcceptedAsync(fixture.Provider, fixture.Tenant, engagement, view,
            ActorReference.ForMember(RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4()), "Synthetic client"),
            AcceptedStaffFixture.Now.AddMinutes(30));
        var before = (await fixture.SourceRecordsAsync()).Count;
        var context = new RequestDispatchContext(
            RequestActor.CreateSystem("reactor:FirmStaffStatusReevaluationV1", "bdgrz.system"), new DirectInvocation(),
            new RequestMetadata(command.CommandId, Uuid.CreateVersion4(), command.DirectoryEventId));

        // Act
        var redelivered = await fixture.Services.GetRequiredService<IRequestBus>().DispatchAsync(command, context);

        // Assert
        Assert.True(redelivered.IsSuccess, redelivered.Error?.Message);
        Assert.Equal(before, (await fixture.SourceRecordsAsync()).Count);
        Assert.Equal(first.ReevaluationContent, Assert.Single(await fixture.ReceiptsAsync()).ReevaluationContent);
    }

    static async Task<RecordDirectoryIndependenceReevaluation> CommandAsync(AcceptedStaffFixture fixture)
    {
        var accepted = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var statuses = new List<DomainEventRecord>();
        await foreach (var source in fixture.Services.GetRequiredService<IDomainEventReader>().ReadAsync(
                           new FirmStaffRecord(fixture.Lead.StaffMemberId).Stream, 0))
            if (source.Event is FirmStaffChangeRecorded { ChangeKind: "status" })
                statuses.Add(source);
        var status = statuses.Last();
        var change = (FirmStaffChangeRecorded)status.Event;
        var root = ((INativeOperationFact)accepted.Event).Operation.RootOperationId;
        return new(RecordDirectoryIndependenceReevaluation.CommandIdOf(status.Event.Metadata.EventId,
                accepted.Event.Metadata.EventId, fixture.Lead.StaffMemberId, fixture.Lead.UserId), fixture.Tenant,
            status.ResourceOffset, status.Event.Metadata.EventId, IndependenceSourceDigest.DirectoryEvent(change),
            accepted.ResourceOffset, accepted.Event.Metadata.EventId, root,
            IndependenceSourceDigest.AcceptanceSource(accepted.Event), fixture.Lead.StaffMemberId, fixture.Lead.UserId,
            Uuid.Parse(accepted.Stream.Resource, CultureInfo.InvariantCulture), 0);
    }
}
