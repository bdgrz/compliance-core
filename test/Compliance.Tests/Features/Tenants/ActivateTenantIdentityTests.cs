using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ActivateTenantIdentityTests
{
    [Fact]
    public async Task ShouldRejectChangedOriginalBootstrapGivenSameActivationIdentityAfterSuccess()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var id = Uuid.CreateVersion4();
        var first = Command(id, fixture.TenantId, fixture.UserId);
        var handler = fixture.Handler();
        Assert.True((await handler.HandleAsync(Fixture.Context(first, id), CancellationToken.None)).IsSuccess);

        // Act
        var result = await handler.HandleAsync(Fixture.Context(Command(id, fixture.TenantId, Uuid.CreateVersion4()), id),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        var current = await fixture.Reader.HydrateAsync(new Tenant(fixture.TenantId));
        Assert.True(current.IsActive);
        Assert.Equal(3UL, current.CommittedStreamPosition);
    }

    // This adapter lets the behavioral RED run before the typed CommandId constructor exists. The old shape
    // uses the same explicit execution identity; the new shape additionally carries that same id in the command.
    static ActivateTenant Command(Uuid commandId, Uuid tenantId, Uuid userId)
    {
        var constructor = typeof(ActivateTenant).GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(parameter => Value(parameter.Name!)).ToArray();
        return (ActivateTenant)constructor.Invoke(arguments);

        object? Value(string name) => name switch
        {
            "CommandId" => commandId,
            "TenantId" => tenantId,
            "FirstAdministratorUserId" => userId,
            "FirstAdministratorEmail" => null,
            _ => throw new InvalidOperationException("Unexpected activation constructor parameter."),
        };
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateWriter _writer;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(new InMemoryKvClient()));
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Members());
            services.AddSingleton<IPermissionAuthorizer>(new Permissions());
            services.AddScoped<NativeOperationExecutor>();
            services.AddPortia();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            _writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }
        public ActivateTenantHandler Handler() => ActivatorUtilities.CreateInstance<ActivateTenantHandler>(_scope.ServiceProvider);
        public static Cntryl.Portia.RequestContext<ActivateTenant> Context(ActivateTenant request, Uuid id) =>
            new(request, new RequestDispatchContext(RequestActor.System, metadata: new RequestMetadata(id, id)));
        public async Task SeedAsync()
        {
            var context = new RequestDispatchContext(RequestActor.System);
            var tenant = new Tenant(TenantId);
            Assert.True(tenant.Register(UserId, "Company", "company", creatorIsAdministrator: true).IsSuccess);
            Assert.True(tenant.ConfirmSlug("company").IsSuccess);
            await _writer.SaveAsync(tenant, context);
            var member = new Member(TenantId, UserId);
            Assert.True(member.Register().IsSuccess);
            await _writer.SaveAsync(member, context);
            var assignment = new TeamMember(TenantId, BuiltInRbac.AdministratorsTeamId(TenantId), RbacIds.Member(TenantId, UserId));
            Assert.True(assignment.Assign(member.MembershipEpisodeId).IsSuccess);
            await _writer.SaveAsync(assignment, context);
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
    sealed class Members : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(new(userId, Uuid.Parse(tenantId, null), "client_personnel"));
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }
}
