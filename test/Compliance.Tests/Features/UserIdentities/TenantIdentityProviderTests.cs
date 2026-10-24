using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class TenantIdentityProviderTests
{
    [Theory]
    [InlineData("http://idp.example.test")]
    [InlineData("https://user:secret@idp.example.test")]
    [InlineData("https://idp.example.test/?tenant=a")]
    [InlineData("https://idp.example.test/#fragment")]
    [InlineData("not-a-uri")]
    public void ShouldRejectIssuerGivenAnInsecureOrAmbiguousIdentity(string issuer)
    {
        // Arrange
        string[] audiences = ["client-app"];

        // Act
        var normalized = TenantIdentityProvider.NormalizeConfiguration(issuer, "client-app", "vault://tenant/idp/v1", audiences);

        // Assert
        Assert.Null(normalized);
    }

    [Fact]
    public void ShouldRequireTheClientAudienceAndDistinctBoundedAudiencesGivenConfiguration()
    {
        // Arrange
        const string issuer = "https://idp.example.test";
        const string credential = "vault://tenant/idp/v1";

        // Act
        var missingClient = TenantIdentityProvider.NormalizeConfiguration(issuer, "client-app", credential, ["api://other"]);
        var duplicate = TenantIdentityProvider.NormalizeConfiguration(issuer, "client-app", credential,
            ["client-app", "client-app"]);
        var none = TenantIdentityProvider.NormalizeConfiguration(issuer, "client-app", credential, []);
        var tooMany = TenantIdentityProvider.NormalizeConfiguration(issuer, "client-app", credential,
            Enumerable.Range(0, 17).Select(index => index == 0 ? "client-app" : $"api://{index}").ToArray());
        var valid = TenantIdentityProvider.NormalizeConfiguration(" https://idp.example.test ", " client-app ", credential,
            [" client-app ", "api://compliance"]);

        // Assert
        Assert.Null(missingClient);
        Assert.Null(duplicate);
        Assert.Null(none);
        Assert.Null(tooMany);
        Assert.Equal(["api://compliance", "client-app"], valid!.AllowedAudiences);
        Assert.Equal("client-app", valid.ClientId);
    }

    [Theory]
    [InlineData("plain-secret")]
    [InlineData("https://vault.example.test/secret")]
    [InlineData("vault://host/path?token=abc")]
    [InlineData("vault://user:pw@host/path")]
    [InlineData("vault://host/path#fragment")]
    public void ShouldRejectCredentialReferenceGivenAnythingButASecretStorePointer(string reference)
    {
        // Arrange

        // Act
        var valid = TenantIdentityProvider.ValidCredentialReference(reference);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void ShouldAcceptOnlyTheKnownSafeFailuresGivenATestResult()
    {
        // Arrange
        var known = new TenantIdentityProviderTestResultContent("check", "failed", "issuer_mismatch",
            "The discovered issuer does not match the configured issuer.");

        // Act
        var accepted = TenantIdentityProvider.NormalizeTestResult(known);
        var freeText = TenantIdentityProvider.NormalizeTestResult(known with { ActionableMessage = "Contact support at 10.0.0.4" });
        var unknownCode = TenantIdentityProvider.NormalizeTestResult(known with { FailureCode = "token_leaked" });
        var passedWithDetail = TenantIdentityProvider.NormalizeTestResult(new TenantIdentityProviderTestResultContent("check",
            "passed", "invalid_client", null));

        // Assert
        Assert.NotNull(accepted);
        Assert.Null(freeText);
        Assert.Null(unknownCode);
        Assert.Null(passedWithDetail);
    }

    [Fact]
    public void ShouldHaveNoStateGivenAConnectionThatWasNeverConfigured()
    {
        // Arrange
        var provider = new TenantIdentityProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var test = provider.PrepareRequestTest(0, "check", Uuid.CreateVersion4(), "Administrator", DateTimeOffset.UtcNow);
        var rotate = provider.PrepareDisable(0, "Retired.", Uuid.CreateVersion4(), "Administrator", DateTimeOffset.UtcNow);

        // Assert
        Assert.False(provider.IsConfigured);
        Assert.Equal("unconfigured", provider.Status);
        Assert.NotNull(test);
        Assert.NotNull(rotate);
    }
}
