using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.UserIdentities.Authorizers;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class ContinueWithOidcProviderAuthorizerTests
{
    [Fact]
    public async Task ShouldAllowActorGivenAuthenticatedOidcIssuerAndSubject()
    {
        // Arrange
        var authorizer = new ContinueWithOidcProviderAuthorizer();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")], "oidc"));
        var context = new RequestContext<ContinueWithOidcProvider>(new ContinueWithOidcProvider(), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectActorGivenMissingAuthenticatedOidcSubject()
    {
        // Arrange
        var authorizer = new ContinueWithOidcProviderAuthorizer();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "https://issuer.example/")], "oidc"));
        var context = new RequestContext<ContinueWithOidcProvider>(new ContinueWithOidcProvider(), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectActorGivenUnauthenticatedOidcIssuerAndSubject()
    {
        // Arrange
        var authorizer = new ContinueWithOidcProviderAuthorizer();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "https://issuer.example/"), new Claim("sub", "provider-subject")]));
        var context = new RequestContext<ContinueWithOidcProvider>(new ContinueWithOidcProvider(), actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Unauthorized, result.Error.Kind);
    }
}
