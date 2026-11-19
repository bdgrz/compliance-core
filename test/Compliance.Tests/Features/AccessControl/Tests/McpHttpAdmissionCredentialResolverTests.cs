using System.Reflection;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Authentication;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class McpHttpAdmissionCredentialResolverTests
{
    [Fact]
    public async Task ShouldResolveGivenSameScopedStatelessMcpPostAndActor()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fixture.User, result.UserId);
        Assert.Equal(fixture.Identity, result.IdentityId);
        Assert.True(fixture.Resolver.IsQualified);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("path")]
    [InlineData("method")]
    [InlineData("direct")]
    [InlineData("stateful")]
    [InlineData("scope")]
    public async Task ShouldRemainUnavailableGivenUnboundMcpIngress(string failure)
    {
        // Arrange
        var fixture = new Fixture();
        var context = fixture.Context();
        if (failure == "actor")
            context = fixture.Context(fixture.Principal(Uuid.CreateVersion4(), fixture.Identity));
        if (failure == "path")
            fixture.Http.Request.Path = "/other";
        if (failure == "method")
            fixture.Http.Request.Method = "GET";
        if (failure == "direct")
            context = new RequestDispatchContext(fixture.Actor);
        if (failure == "stateful")
            fixture.Transport.SessionMode = HttpServerSessionMode.Stateful;
        if (failure == "scope")
            fixture.SetServices(includeResolver: false);

        // Act
        var result = await fixture.Resolver.ResolveAsync(context, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldLoseQualificationGivenCurrentRequestOrActorChanges()
    {
        // Arrange
        var fixture = new Fixture();
        Assert.NotNull(await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None));

        // Act
        fixture.Http.User = fixture.Principal(fixture.User, fixture.Identity);

        // Assert
        Assert.False(fixture.Resolver.IsQualified);
    }

    [Fact]
    public async Task ShouldKeepConcurrentRequestActorsInTheirOwnScopes()
    {
        // Arrange
        var first = new Fixture();
        var second = new Fixture();

        // Act
        var results = await Task.WhenAll(Task.Run(async () =>
        {
            first.Accessor.HttpContext = first.Http;
            return await first.Resolver.ResolveAsync(first.Context(), CancellationToken.None);
        }), Task.Run(async () =>
        {
            second.Accessor.HttpContext = second.Http;
            return await second.Resolver.ResolveAsync(second.Context(), CancellationToken.None);
        }));

        // Assert
        Assert.Equal(first.User, results[0]!.UserId);
        Assert.Equal(second.User, results[1]!.UserId);
        Assert.NotEqual(results[0]!.IdentityId, results[1]!.IdentityId);
    }

    sealed class Fixture
    {
        static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Identity { get; } = Uuid.CreateVersion4();
        public DefaultHttpContext Http { get; } = new();
        public HttpContextAccessor Accessor { get; } = new();
        public ClaimsPrincipal Actor { get; }
        public HttpServerTransportOptions Transport { get; } = new() { SessionMode = HttpServerSessionMode.Stateless };
        public ITrustedAdmissionCredentialResolver Resolver { get; }

        public Fixture()
        {
            Actor = Principal(User, Identity);
            Http.Request.Method = "POST";
            Http.Request.Path = "/mcp";
            Http.User = Actor;
            Accessor.HttpContext = Http;
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["BDGRZ_SESSION_SIGNING_KEY"] = new string('s', 48) }).Build();
            var resolve = typeof(BdgrzSessionTokens).GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic)!;
            var tokens = (BdgrzSessionTokens)resolve.Invoke(null, [config, false])!;
            var type = typeof(BdgrzSessionTokens).Assembly.GetType("Bdgrz.Compliance.Features.Authentication.McpHttpAdmissionCredentialResolver", true)!;
            Resolver = (ITrustedAdmissionCredentialResolver)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [Accessor, tokens, new DeveloperUserRegistration(false), new FixedClock(), Options.Create(Transport), new Environment()], null)!;
            SetServices(includeResolver: true);
        }

        public void SetServices(bool includeResolver)
        {
            var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication(Actor));
            if (includeResolver)
                services.AddSingleton(Resolver.GetType(), Resolver);
            Http.RequestServices = services.BuildServiceProvider();
        }

        public ClaimsPrincipal Principal(Uuid user, Uuid identity) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()), new Claim("user_identity_id", identity.ToString()),
             new Claim("nbf", Now.AddMinutes(-1).ToUnixTimeSeconds().ToString()),
             new Claim("exp", Now.AddMinutes(1).ToUnixTimeSeconds().ToString())], "BdgrzSession"));

        public IRequestContext Context(ClaimsPrincipal? actor = null) =>
            new RequestDispatchContext(actor ?? Actor, new McpInvocation("test_tool"));
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    }

    sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    sealed class Authentication(ClaimsPrincipal principal) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "BdgrzSession")));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
