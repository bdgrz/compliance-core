using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

/// <summary>
///     The identified native operations of declared applications and system instances: an exact retry replays the
///     original result after any later owner change, and any change of actor, authored intent or original revision
///     under the same identity conflicts without an append.
/// </summary>
public sealed class ApplicationInventoryOperationTests
{
    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactReviseRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var first = Revise(host, applicationId, 1, "Payroll v2");
        Assert.True((await host.Revise.HandleAsync(host.Context(first), CancellationToken.None)).IsSuccess);
        var later = Revise(host, applicationId, 2, "Payroll v3");
        Assert.True((await host.Revise.HandleAsync(host.Context(later), CancellationToken.None)).IsSuccess);
        var before = await host.ApplicationEventsAsync(applicationId);

        // Act
        var retry = await host.Revise.HandleAsync(host.Context(first), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before, await host.ApplicationEventsAsync(applicationId));
        Assert.Equal(3, (await host.ApplicationAsync(applicationId)).Revision);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("expected_revision")]
    [InlineData("actor")]
    public async Task ShouldConflictWithoutAppendGivenSameReviseCommandWithChangedActorIntentOrOriginalRevision(
        string changed)
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var original = Revise(host, applicationId, 1, "Payroll v2");
        Assert.True((await host.Revise.HandleAsync(host.Context(original), CancellationToken.None)).IsSuccess);
        var before = await host.ApplicationEventsAsync(applicationId);
        var retry = changed switch
        {
            "name" => original with { Name = "Another name" },
            "expected_revision" => original with { ExpectedRevision = 2 },
            _ => original,
        };
        var actor = changed == "actor" ? ApplicationInventoryHost.ActorFor(Uuid.CreateVersion4()) : null;

        // Act
        var result = await host.Revise.HandleAsync(host.Context(retry, actor), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, result.Error!.Message);
        Assert.Equal(before, await host.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMissingOrMalformedReviseIdentity()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var before = await host.ApplicationEventsAsync(applicationId);

        // Act
        var noCommand = await host.Revise.HandleAsync(host.Context(
            Revise(host, applicationId, 1, "Payroll v2") with { CommandId = Uuid.Empty }), CancellationToken.None);
        var noApplication = await host.Revise.HandleAsync(host.Context(
            Revise(host, applicationId, 1, "Payroll v2") with { ApplicationId = Uuid.Empty }), CancellationToken.None);
        var negative = await host.Revise.HandleAsync(host.Context(
            Revise(host, applicationId, -1, "Payroll v2")), CancellationToken.None);

        // Assert
        Assert.All([noCommand, noApplication, negative],
            result => Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind));
        Assert.Equal(before, await host.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenStaleReviseExpectedRevision()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        Assert.True((await host.Revise.HandleAsync(host.Context(Revise(host, applicationId, 1, "Payroll v2")),
            CancellationToken.None)).IsSuccess);
        var before = await host.ApplicationEventsAsync(applicationId);

        // Act
        var stale = await host.Revise.HandleAsync(host.Context(Revise(host, applicationId, 1, "Payroll v3")),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(before, await host.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public async Task ShouldWriteOneRevisionGivenConcurrentIdenticalRevisions()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var gate = new HydrationGate<DeclaredApplication>(host.Reader, participants: 2);
        var handler = new ReviseApplicationHandler(new NativeOperationExecutor(gate,
                host.Services.GetRequiredService<IAggregateExecutor>(),
                host.Services.GetRequiredService<IImmutableContentStore>()), gate,
            host.Services.GetRequiredService<IImmutableContentStore>(), TimeProvider.System);
        var request = Revise(host, applicationId, 1, "Payroll v2");

        // Act
        var racing = await Task.WhenAll(
            handler.HandleAsync(host.Context(request), CancellationToken.None).AsTask(),
            handler.HandleAsync(host.Context(request), CancellationToken.None).AsTask());

        // Assert: the loser rehydrates, resolves the same command id and replays the winner.
        Assert.All(racing, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(2, (await host.ApplicationEventsAsync(applicationId)).Count);
    }

    [Fact]
    public async Task ShouldRecoverWithoutDuplicateAppendGivenUnknownAppendOutcome()
    {
        // Arrange: the revision commits but its response is lost, so the caller cannot tell it happened.
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var content = host.Services.GetRequiredService<IImmutableContentStore>();
        var lossy = new ReviseApplicationHandler(new NativeOperationExecutor(host.Reader,
                new LostResponseExecutor(host.Services.GetRequiredService<IAggregateExecutor>()), content),
            host.Reader, content, TimeProvider.System);
        var request = Revise(host, applicationId, 1, "Payroll v2");

        // Act
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await lossy.HandleAsync(host.Context(request), CancellationToken.None));
        var afterLoss = await host.ApplicationEventsAsync(applicationId);
        var retry = await host.Revise.HandleAsync(host.Context(request), CancellationToken.None);

        // Assert: the identical retry resolves the committed fact and appends nothing.
        Assert.Equal(2, afterLoss.Count);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(afterLoss, await host.ApplicationEventsAsync(applicationId));
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactRetireRetryAfterRetirement()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var retire = new RetireApplication(Uuid.CreateVersion4(), host.Tenant, applicationId, 1,
            ApplicationInventoryHost.Now, "Decommissioned");
        Assert.True((await host.Retire.HandleAsync(host.Context(retire), CancellationToken.None)).IsSuccess);
        var before = await host.ApplicationEventsAsync(applicationId);

        // Act
        var retry = await host.Retire.HandleAsync(host.Context(retire), CancellationToken.None);
        var changed = await host.Retire.HandleAsync(host.Context(retire with { Reason = "Another reason" }),
            CancellationToken.None);
        var other = await host.Retire.HandleAsync(host.Context(retire with { CommandId = Uuid.CreateVersion4() }),
            CancellationToken.None);

        // Assert: the original result replays, a changed intent conflicts, and a new command meets the retired state.
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, changed.Error!.Message);
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.Equal("The application is retired.", other.Error!.Message);
        Assert.Equal(before, await host.ApplicationEventsAsync(applicationId));
        Assert.True((await host.ApplicationAsync(applicationId)).IsRetired);
    }

    [Fact]
    public async Task ShouldRetireWithTrimmedReasonGivenWhitespaceVariantRetry()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var retire = new RetireApplication(Uuid.CreateVersion4(), host.Tenant, applicationId, 1,
            ApplicationInventoryHost.Now, " Decommissioned ");
        Assert.True((await host.Retire.HandleAsync(host.Context(retire), CancellationToken.None)).IsSuccess);

        // Act
        var retry = await host.Retire.HandleAsync(host.Context(retire with { Reason = "Decommissioned" }),
            CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        var retired = Assert.IsType<ApplicationRetired>((await host.ApplicationEventsAsync(applicationId))[^1].Event);
        Assert.Equal("Decommissioned", retired.Reason);
        Assert.Equal(retire.CommandId, retired.Operation.LogicalId);
        Assert.Equal(1UL, retired.Operation.OriginalExpectedRevision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactSystemInstanceRetriesAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var instanceId = Uuid.CreateVersion4();
        var declare = new DeclareSystemInstance(instanceId, host.Tenant, applicationId, 1, "Production", "production",
            "Boundary v1", "payroll-prod");
        var revise = new ReviseSystemInstance(Uuid.CreateVersion4(), host.Tenant, applicationId, instanceId, 1,
            "Production east", "payroll-prod-east", "Boundary v2", "Moved to the east network zone.");
        var retire = new RetireSystemInstance(Uuid.CreateVersion4(), host.Tenant, applicationId, instanceId, 2,
            ApplicationInventoryHost.Now, "Decommissioned");
        Assert.True((await host.DeclareInstance.HandleAsync(host.Context(declare), CancellationToken.None)).IsSuccess);
        Assert.True((await host.ReviseInstance.HandleAsync(host.Context(revise), CancellationToken.None)).IsSuccess);
        Assert.True((await host.RetireInstance.HandleAsync(host.Context(retire), CancellationToken.None)).IsSuccess);
        var before = await host.InstanceEventsAsync(instanceId);

        // Act
        var declaredAgain = await host.DeclareInstance.HandleAsync(host.Context(declare), CancellationToken.None);
        var revisedAgain = await host.ReviseInstance.HandleAsync(host.Context(revise), CancellationToken.None);
        var retiredAgain = await host.RetireInstance.HandleAsync(host.Context(retire), CancellationToken.None);

        // Assert
        Assert.True(declaredAgain.IsSuccess, declaredAgain.Error?.Message);
        Assert.Equal(instanceId, declaredAgain.Value!.SystemInstanceId);
        Assert.True(revisedAgain.IsSuccess, revisedAgain.Error?.Message);
        Assert.True(retiredAgain.IsSuccess, retiredAgain.Error?.Message);
        Assert.Equal(before, await host.InstanceEventsAsync(instanceId));
        var instance = await host.InstanceAsync(instanceId);
        Assert.True(instance.IsRetired);
        Assert.Equal(3, instance.Revision);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSystemInstanceIdentityWithChangedIntent()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var instanceId = Uuid.CreateVersion4();
        var declare = new DeclareSystemInstance(instanceId, host.Tenant, applicationId, 1, "Production", "production");
        var revise = new ReviseSystemInstance(Uuid.CreateVersion4(), host.Tenant, applicationId, instanceId, 1,
            "Production east", "payroll-prod-east", "Boundary v2", "Moved.");
        Assert.True((await host.DeclareInstance.HandleAsync(host.Context(declare), CancellationToken.None)).IsSuccess);
        Assert.True((await host.ReviseInstance.HandleAsync(host.Context(revise), CancellationToken.None)).IsSuccess);
        var before = await host.InstanceEventsAsync(instanceId);

        // Act
        var otherKind = await host.DeclareInstance.HandleAsync(host.Context(declare with { Kind = "staging" }),
            CancellationToken.None);
        var otherParent = await host.DeclareInstance.HandleAsync(host.Context(declare with
        {
            ApplicationId = Uuid.CreateVersion4(),
        }), CancellationToken.None);
        var otherRationale = await host.ReviseInstance.HandleAsync(host.Context(revise with { Rationale = "Other." }),
            CancellationToken.None);
        var otherActor = await host.ReviseInstance.HandleAsync(host.Context(revise,
            ApplicationInventoryHost.ActorFor(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, otherKind.Error?.Message);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, otherRationale.Error?.Message);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, otherActor.Error?.Message);
        Assert.Equal(AcceptedAppendResult.ConflictError.Message, otherParent.Error?.Message);
        Assert.Equal(before, await host.InstanceEventsAsync(instanceId));
    }

    [Fact]
    public async Task ShouldRecordNothingGivenSystemInstanceRevisionThatChangesNothing()
    {
        // Arrange
        await using var host = new ApplicationInventoryHost();
        var applicationId = await host.DeclareApplicationAsync();
        var instanceId = Uuid.CreateVersion4();
        var declare = new DeclareSystemInstance(instanceId, host.Tenant, applicationId, 1, "Production", "production",
            "Boundary v1", "payroll-prod");
        Assert.True((await host.DeclareInstance.HandleAsync(host.Context(declare), CancellationToken.None)).IsSuccess);
        var unchanged = new ReviseSystemInstance(Uuid.CreateVersion4(), host.Tenant, applicationId, instanceId, 1,
            "Production", "payroll-prod", "Boundary v1", "No change.");

        // Act
        var result = await host.ReviseInstance.HandleAsync(host.Context(unchanged), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(await host.InstanceEventsAsync(instanceId));
    }

    [Fact]
    public void ShouldBindLogicalIdentityToTheDeclaredFieldGivenIdentifiedInventoryCommands()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var tenant = Uuid.CreateVersion4();
        var application = Uuid.CreateVersion4();

        // Act
        IIdentifiedRequest[] identified =
        [
            new DeclareApplication(id, tenant, "Payroll", "Run payroll"),
            new ReviseApplication(id, tenant, application, 1, "Payroll", "Run payroll", null),
            new RetireApplication(id, tenant, application, 1, ApplicationInventoryHost.Now, "Done"),
            new DeclareSystemInstance(id, tenant, application, 1, "Production", "production"),
            new ReviseSystemInstance(id, tenant, application, application, 1, "Production", "source", "boundary", "Why"),
            new RetireSystemInstance(id, tenant, application, application, 1, ApplicationInventoryHost.Now, "Done"),
        ];

        // Assert
        Assert.All(identified, request => Assert.Equal(id, request.RequestId));
    }

    static ReviseApplication Revise(ApplicationInventoryHost host, Uuid applicationId, long expectedRevision,
        string name) => new(Uuid.CreateVersion4(), host.Tenant, applicationId, expectedRevision, name, "Run payroll",
        "Finance");
}
