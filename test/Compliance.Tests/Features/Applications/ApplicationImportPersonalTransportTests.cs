using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPersonalTransportTests
{
    [Theory]
    [InlineData("correlate", false)]
    [InlineData("correlate", true)]
    [InlineData("accept", false)]
    [InlineData("accept", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    public async Task ShouldRetainExactImportSourceGivenNonHttpPersonalDecision(string operation, bool mcp)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batch = await fixture.StageAsync();
        if (operation == "accept")
            Assert.True((await fixture.RunAsync("correlate", batch, Fixture.Http)).IsSuccess);
        var before = await fixture.BatchAsync(batch.Id);

        // Act
        var result = await fixture.RunAsync(operation, batch,
            mcp ? new McpInvocation("synthetic.import.decision") : null);
        var retained = await fixture.BatchAsync(batch.Id);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(before.Revision, retained.Revision);
        Assert.Equal(before.State, retained.State);
        Assert.Null(retained.Plan);
        Assert.Null(retained.CancellationAccepted);
    }

    [Theory]
    [InlineData("correlate")]
    [InlineData("accept")]
    [InlineData("cancel")]
    public async Task ShouldRetainAttributedImportDecisionGivenNativePersonalHttp(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batch = await fixture.StageAsync();
        if (operation == "accept")
            Assert.True((await fixture.RunAsync("correlate", batch, Fixture.Http)).IsSuccess);
        var before = await fixture.BatchAsync(batch.Id);

        // Act
        var result = await fixture.RunAsync(operation, batch, Fixture.Http);
        var retained = await fixture.BatchAsync(batch.Id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retained.CommittedStreamPosition > before.CommittedStreamPosition);
        Assert.Equal(operation == "accept" ? "accepting" : operation == "cancel" ? "canceling" : "preview_ready",
            retained.State);
        var member = RbacIds.Member(fixture.Tenant, fixture.User);
        if (operation == "accept")
            Assert.Equal(member.ToString(), retained.Plan!.Operation.Actor.Id);
        if (operation == "cancel")
            Assert.Equal(member.ToString(), retained.CancellationAccepted!.Operation.Actor.Id);
        if (operation == "correlate")
            Assert.Equal(member, retained.GetCorrelation(ApplicationImportEffects.RowId(batch.Id, 1))!.ActorMemberId);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateReader _reader;

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<ITenantActivity>(new Activity());
            services.AddSingleton<IPermissionAuthorizer>(new Permissions());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(Tenant, User));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            _reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public static HttpInvocation Http => new("PUT", "/synthetic/import", "/synthetic/import", "synthetic");

        public Uuid Tenant { get; } = Uuid.CreateVersion4();

        public Uuid User { get; } = Uuid.CreateVersion4();

        public System.Security.Claims.ClaimsPrincipal Actor => ProgramManagementServices.Actor(User);

        public IRequestBus Bus { get; }

        public async Task<ApplicationImportBatch> StageAsync()
        {
            var staged = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "transport", "partial", [new("app", "Application", "Purpose", null)]), Actor);
            Assert.True(staged.IsSuccess, staged.Error?.Message);
            return await _reader.HydrateAsync(new ApplicationImportBatch(Tenant, staged.Value!.BatchId));
        }

        /// <summary>Runs one personal decision; a null invocation sends it with no transport intent.</summary>
        public async Task<Result> RunAsync(string operation, ApplicationImportBatch batch, RequestInvocation? invocation)
        {
            var context = new RequestDispatchContext(Actor, invocation);
            switch (operation)
            {
                case "correlate":
                    return await Bus.DispatchAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(), Tenant,
                        batch.Id, ApplicationImportEffects.RowId(batch.Id, 1), 1, "create_new", null, null,
                        "Reviewed source identity"), context, CancellationToken.None);
                case "accept":
                    var accepted = await Bus.DispatchAsync(new AcceptApplicationImport(Uuid.CreateVersion4(), Tenant,
                        batch.Id, 2), context, CancellationToken.None);
                    return accepted.IsSuccess ? Result.Success : Result.Failure(accepted.Error!);
                default:
                    var canceled = await Bus.DispatchAsync(new CancelApplicationImport(Uuid.CreateVersion4(), Tenant,
                        batch.Id, 1, "Personal cancellation"), context, CancellationToken.None);
                    return canceled.IsSuccess ? Result.Success : Result.Failure(canceled.Error!);
            }
        }

        public ValueTask<ApplicationImportBatch> BatchAsync(Uuid batchId) =>
            _reader.HydrateAsync(new ApplicationImportBatch(Tenant, batchId, selectedRowId: ApplicationImportEffects.RowId(batchId, 1)));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Activity : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission == RbacPermissions.ApplicationInventoryManage);
    }

    sealed class Memberships(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user
                ? new TenantMembershipView(user, tenant, "client_personnel") : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
