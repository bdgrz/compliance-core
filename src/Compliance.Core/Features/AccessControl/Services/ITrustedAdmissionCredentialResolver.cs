using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Trusted authentication boundary for source admission; no request-body claims are authority.</summary>
/// <remarks>
/// A qualified implementation cryptographically authenticates the exact principal and supplies
/// the authenticated human identity, user, scheme, validity interval and any applicable session.
/// The Bdgrz identity-only mode additionally certifies the same validated token's exact
/// user_identity_id binding; it does not assert anything about current source state. Null is
/// unavailable, never an authenticated denial. Qualification must remain valid throughout admission.
/// </remarks>
interface ITrustedAdmissionCredentialResolver
{
    bool IsQualified { get; }
    ValueTask<AccessAdmissionSemanticEvaluator.Credential?> ResolveAsync(IRequestContext context, CancellationToken ct);
}
