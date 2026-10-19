using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class GetTenantAuthorizerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();

    [Theory]
    [InlineData(true, false, true, true, null)]
    [InlineData(false, false, true, true, RequestErrorKind.NotFound)]
    [InlineData(true, false, false, true, RequestErrorKind.Forbidden)]
    [InlineData(true, false, true, false, RequestErrorKind.Forbidden)]
    [InlineData(true, true, true, true, RequestErrorKind.NotFound)]
    public async Task ShouldRequireMembershipActivityAndPermissionGivenMemberAccess(
        bool member, bool suspended, bool active, bool permitted, RequestErrorKind? expectedError)
    {
        // Arrange
        var authorizer = new GetTenantAuthorizer(new FixedOperatorAccess(),
            new Memberships(member, suspended), new TenantActivity(active), new Permissions(permitted));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<GetTenant>(new GetTenant(TenantId), Actor()), CancellationToken.None);

        // Assert
        if (expectedError is null)
            Assert.True(result.IsSuccess);
        else
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedError, result.Error.Kind);
        }
    }

    [Fact]
    public async Task ShouldAllowOperatorReadGivenNoTenantMembership()
    {
        // Arrange
        var authorizer = new GetTenantAuthorizer(new FixedOperatorAccess(UserId),
            new Memberships(false), new TenantActivity(false), new Permissions(false));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<GetTenant>(new GetTenant(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldHideTenantGivenExpiredGuestMembership()
    {
        // Arrange
        var membership = new Memberships(true, expiresOn: DateOnly.FromDateTime(DateTime.UtcNow)
            .AddDays(-1));
        var authorizer = new GetTenantAuthorizer(new FixedOperatorAccess(), membership,
            new TenantActivity(true), new Permissions(true));

        // Act
        var result = await authorizer.AuthorizeAsync(
            new RequestContext<GetTenant>(new GetTenant(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString())], "BdgrzSession"));

    sealed class Memberships(bool member, bool suspended = false, DateOnly? expiresOn = null)
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(member
            ? new TenantMembershipView(userId, TenantId, Affiliation: expiresOn is null
                    ? "client_personnel" : "guest", IsSuspended: suspended,
                MembershipExpiresOn: expiresOn) : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class TenantActivity(bool active) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(active);
    }

    sealed class Permissions(bool permitted) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permitted);
    }
}
