using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Exports;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using ClientExportAggregate = Bdgrz.Compliance.Features.Exports.ClientExport;

namespace Bdgrz.Compliance.Tests.Features.Exports;

public sealed class RecordClientExportDeliveryHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordAttributedDeliveryReceiptGivenBuiltExport()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedExportAsync(built: true);
        var command = fixture.Command(fixture.ExportRevision);

        // Act
        var result = await fixture.DispatchAsync(command, fixture.Actor);
        var retry = await fixture.DispatchAsync(command, fixture.Actor);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("delivered", saved.Current!.Status);
        Assert.True(saved.Current.IsComplete);
        Assert.Equal(fixture.ExportRevision + 1, saved.Revision);
        Assert.Equal("attest-team", saved.Current.Delivery!.Recipient);
        Assert.Equal("delivery-receipt-42", saved.Current.Delivery.DeliveryReference);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId),
            saved.Current.Delivery.RecordedByMemberId);
        Assert.Equal(fixture.ArchiveId, saved.Current.Delivery.ArchiveArtifactId);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenDeliveryCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedExportAsync(built: true);

        // Act
        var result = await fixture.DispatchAsync(fixture.Command(fixture.ExportRevision),
            RequestActor.System);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("built", saved.Current!.Status);
        Assert.Equal(fixture.ExportRevision, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectDeliveryBeforeBuildGivenExportRequest()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedExportAsync(built: false);

        // Act
        var result = await fixture.DispatchAsync(fixture.Command(fixture.ExportRevision), fixture.Actor);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("requested", saved.Current!.Status);
        Assert.Equal(fixture.ExportRevision, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionAndFutureDeliveryTimeGivenBuiltExport()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedExportAsync(built: true);

        // Act
        var stale = await fixture.DispatchAsync(fixture.Command(fixture.ExportRevision - 1), fixture.Actor);
        var future = await fixture.DispatchAsync(fixture.Command(fixture.ExportRevision) with
        {
            DeliveredAt = Now.AddMinutes(1),
        }, fixture.Actor);
        var saved = await fixture.Reader.HydrateAsync(new ClientExportAggregate(fixture.TenantId,
            fixture.ExportId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, future.Error?.Kind);
        Assert.Equal("built", saved.Current!.Status);
        Assert.Equal(fixture.ExportRevision, saved.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateWriter _writer;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ExportId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid SnapshotId { get; } = Uuid.CreateVersion4();
        public Uuid ArchiveId { get; } = Uuid.CreateVersion4();
        public long ExportRevision { get; private set; }
        public IAggregateReader Reader { get; }
        public IRequestBus Bus { get; }
        public ClaimsPrincipal Actor => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
            new Claim("email", "owner@example.test"),
        ], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddPortia()
                .AddRequestHandler<RecordClientExportDeliveryHandler>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        }

        public async Task SeedExportAsync(bool built)
        {
            var export = new ClientExportAggregate(TenantId, ExportId);
            var requester = RbacIds.Member(TenantId, UserId);
            Assert.Null(export.Request(new ClientExportContent("organization", "zip", "offboarding-2026",
                    SnapshotId, new string('a', 64), 1,
                    [new ClientExportRecordContent("tenant_record", Uuid.CreateVersion4(), 1,
                        new string('b', 64), "included", null, null, [])], []),
                requester, "owner@example.test", Now.AddMinutes(-5)));
            if (built)
                Assert.Null(export.RecordBuildResult(export.Revision,
                    new ClientExportBuildResultContent(Uuid.CreateVersion4(), 1, "built", ArchiveId,
                        new string('c', 64), null, Uuid.CreateVersion4(), 1, 0, SnapshotId,
                        new string('a', 64)), requester, "export worker", Now.AddMinutes(-3)));
            await _writer.SaveAsync(export, new RequestDispatchContext(RequestActor.System));
            ExportRevision = export.Revision;
        }

        public RecordClientExportDelivery Command(long expectedRevision) =>
            new(TenantId, ExportId, expectedRevision, "attest-team", Now.AddMinutes(-1),
                "delivery-receipt-42");

        public ValueTask<Result> DispatchAsync(RecordClientExportDelivery command, ClaimsPrincipal actor) =>
            Bus.DispatchAsync(command, new RequestDispatchContext(actor,
                new HttpInvocation("POST", "/api/v1/client-exports/delivery",
                    "/api/v1/client-exports/delivery", "test")));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
