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

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class HttpAdmissionCredentialResolverTests
{
    [Fact]
    public async Task ShouldResolveOriginalTimesGivenTheSameAuthenticatedHttpRequest()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var credential = await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None);

        // Assert
        Assert.NotNull(credential);
        Assert.Equal(fixture.User, credential.UserId);
        Assert.Equal(fixture.Identity, credential.IdentityId);
        Assert.Equal(fixture.Start, credential.NotBefore);
        Assert.Equal(fixture.End, credential.ExpiresAt);
        Assert.True(fixture.Resolver.IsQualified);
    }

    [Fact]
    public async Task ShouldReturnUnavailableGivenDirectDispatchOrMismatchedActorOrRequest()
    {
        // Arrange
        var fixture = new Fixture();
        var other = fixture.Principal(Uuid.CreateVersion4(), fixture.Identity);
        var wrong = new RequestDispatchContext(fixture.Actor, new HttpInvocation("GET", "/other", null, fixture.Http.TraceIdentifier));
        var mcp = new RequestDispatchContext(fixture.Actor, new McpInvocation("read"));

        // Act
        var direct = await fixture.Resolver.ResolveAsync(new RequestDispatchContext(fixture.Actor), CancellationToken.None);
        var spoof = await fixture.Resolver.ResolveAsync(fixture.Context(other), CancellationToken.None);
        var mismatched = await fixture.Resolver.ResolveAsync(wrong, CancellationToken.None);
        var mcpResult = await fixture.Resolver.ResolveAsync(mcp, CancellationToken.None);

        // Assert
        Assert.Null(direct);
        Assert.Null(spoof);
        Assert.Null(mismatched);
        Assert.Null(mcpResult);
    }

    [Fact]
    public async Task ShouldReturnUnavailableGivenFallbackKeyOrDevelopmentBypass()
    {
        // Arrange
        var fallback = new Fixture(fallback: true);
        var bypass = new Fixture(developer: true);

        // Act
        var first = await fallback.Resolver.ResolveAsync(fallback.Context(), CancellationToken.None);
        var second = await bypass.Resolver.ResolveAsync(bypass.Context(), CancellationToken.None);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task ShouldAllowConfiguredDeveloperSessionOnlyInDevelopmentEnvironment()
    {
        // Arrange
        var fixture = new Fixture(fallback: true, developer: true, developmentEnvironment: true);

        // Act
        var result = await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ShouldReturnUnavailableGivenAmbiguousTokenClaims()
    {
        // Arrange
        var fixture = new Fixture();
        ((ClaimsIdentity)fixture.Actor.Identity!).AddClaim(new Claim("exp", fixture.End.ToUnixTimeSeconds().ToString()));

        // Act
        var result = await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldReturnUnavailableGivenExpiredCredentialEvenWithSuccessfulAuthentication()
    {
        // Arrange
        var fixture = new Fixture();
        var identity = (ClaimsIdentity)fixture.Actor.Identity!;
        identity.RemoveClaim(identity.FindFirst("exp")!);
        identity.AddClaim(new Claim("exp", fixture.Start.AddSeconds(1).ToUnixTimeSeconds().ToString()));

        // Act
        var result = await fixture.Resolver.ResolveAsync(fixture.Context(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldPropagateCancellationGivenCancelledRequest()
    {
        // Arrange
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Resolver.ResolveAsync(fixture.Context(), cancellation.Token));
    }

    sealed class Fixture
    {
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Identity { get; } = Uuid.CreateVersion4();
        static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Start { get; } = Now.AddMinutes(-1);
        public DateTimeOffset End { get; } = Now.AddMinutes(1);
        public DefaultHttpContext Http { get; } = new();
        public ClaimsPrincipal Actor { get; }
        public ITrustedAdmissionCredentialResolver Resolver { get; }

        public Fixture(bool fallback = false, bool developer = false, bool developmentEnvironment = false)
        {
            Actor = Principal(User, Identity);
            Http.Request.Method = "GET";
            Http.Request.Path = "/api/example";
            Http.User = Actor;
            Http.RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication(Actor))
                .BuildServiceProvider();
            var config = new ConfigurationBuilder().AddInMemoryCollection(fallback ? [] :
                new Dictionary<string, string?> { ["BDGRZ_SESSION_SIGNING_KEY"] = new string('s', 48) }).Build();
            var resolve = typeof(BdgrzSessionTokens).GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic)!;
            var tokens = (BdgrzSessionTokens)resolve.Invoke(null, [config, fallback])!;
            var type = typeof(BdgrzSessionTokens).Assembly.GetType("Bdgrz.Compliance.Features.Authentication.HttpAdmissionCredentialResolver", true)!;
            Resolver = (ITrustedAdmissionCredentialResolver)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [new HttpContextAccessor { HttpContext = Http }, tokens, new DeveloperUserRegistration(developer), new FixedClock(),
                    new Environment { EnvironmentName = developmentEnvironment ? "Development" : "Production" }], null)!;
        }

        public ClaimsPrincipal Principal(Uuid user, Uuid identity) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString()), new Claim("user_identity_id", identity.ToString()),
             new Claim("nbf", Start.ToUnixTimeSeconds().ToString()), new Claim("exp", End.ToUnixTimeSeconds().ToString())], "BdgrzSession"));

        public IRequestContext Context(ClaimsPrincipal? actor = null) => new RequestDispatchContext(actor ?? Actor,
            new HttpInvocation(Http.Request.Method, Http.Request.Path.Value!, null, Http.TraceIdentifier));
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
