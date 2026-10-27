namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>Safe provider-check result, excluding tokens and response bodies.</summary>
public sealed record TenantIdentityProviderTestResultContent(
    string TestKey,
    string Outcome,
    string? FailureCode,
    string? ActionableMessage);
