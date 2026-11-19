using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Composes a trusted credential, fixed operator route and qualified source capture.</summary>
/// <remarks>
/// This implementation is deliberately unregistered. A source, cryptographic credential adapter,
/// canonical route resolver and supported complete policy must be qualified before use.
/// Program creation requires a separately qualified complete grant-path resolver.
/// Program selection requires a separately qualified complete scoped grant-path resolver.
/// </remarks>
sealed class AccessAdmissionAuthority(
    ITrustedAdmissionCredentialResolver? credentials,
    ITrustedAdmissionOperatorPathResolver? routes,
    AccessAdmissionStableRead.ISource? source,
    TimeProvider clock,
    string supportedPolicyGeneration,
    string supportedPolicyContentSha256,
    ITrustedAdmissionProgramPathResolver? programRoutes = null,
    ITrustedAdmissionProgramSelectionPathResolver? selectionRoutes = null) : IAccessAdmissionAuthority
{
    public async ValueTask<AccessAdmissionEvidence?> AdmitCriteriaOperatorAsync(IRequestContext context,
        Uuid operationId, string target, string intentSha256, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var actor = context.Actor;
        ct.ThrowIfCancellationRequested();
        if (credentials is null || routes is null || source is null ||
            !credentials.IsQualified || !routes.IsQualified || !source.IsFreshnessQualified ||
            operationId == Uuid.Empty || !CanonicalStream(target) || !Digest(intentSha256))
            throw new AccessAdmissionUnavailableException();

        var credential = await credentials.ResolveAsync(context, ct).ConfigureAwait(false);
        if (credential is null || !MatchesPrincipal(actor, credential))
            throw new AccessAdmissionUnavailableException();
        var participants = await routes.ResolveAsync(credential, ct).ConfigureAwait(false);
        if (participants is null || !credentials.IsQualified || !routes.IsQualified || !source.IsFreshnessQualified)
            throw new AccessAdmissionUnavailableException();
        var count = participants.Count;
        if (count is < 3 or > 6)
            throw new AccessAdmissionUnavailableException();
        var fixedPath = new AccessAdmissionStableRead.Participant[count];
        try
        {
            for (var index = 0; index < fixedPath.Length; index++)
                fixedPath[index] = participants[index];
        }
        catch (ArgumentOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        catch (IndexOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        var evaluator = new AccessAdmissionSemanticEvaluator(new AccessAdmissionStableRead(source, clock),
            supportedPolicyGeneration, supportedPolicyContentSha256);
        var candidate = new AccessAdmissionSemanticEvaluator.Candidate(credential, operationId, target, intentSha256, fixedPath);
        var decision = await evaluator.EvaluateOperatorAsync(candidate, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!credentials.IsQualified || !routes.IsQualified || !source.IsFreshnessQualified)
            throw new AccessAdmissionUnavailableException();
        return decision.Status switch
        {
            AccessAdmissionSemanticEvaluator.Status.Allowed when decision.Evidence is not null => decision.Evidence,
            AccessAdmissionSemanticEvaluator.Status.Denied => null,
            _ => throw new AccessAdmissionUnavailableException(),
        };
    }

    public async ValueTask<AccessAdmissionEvidence?> AdmitProgramCreationAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, string target, string intentSha256, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var actor = context.Actor;
        ct.ThrowIfCancellationRequested();
        if (credentials is null || programRoutes is null || source is null || !credentials.IsQualified ||
            !programRoutes.IsQualified || !source.IsFreshnessQualified || tenantId == Uuid.Empty || programId == Uuid.Empty ||
            !CanonicalStream(target) || target != new ComplianceProgram(tenantId, programId).Stream.ToString() || !Digest(intentSha256))
            throw new AccessAdmissionUnavailableException();

        var credential = await credentials.ResolveAsync(context, ct).ConfigureAwait(false);
        if (credential is null || !MatchesPrincipal(actor, credential))
            throw new AccessAdmissionUnavailableException();
        var resolved = await programRoutes.ResolveAsync(credential, tenantId, ct).ConfigureAwait(false);
        if (resolved is null || !credentials.IsQualified || !programRoutes.IsQualified || !source.IsFreshnessQualified ||
            resolved.TenantId != tenantId || resolved.UserId != credential.UserId || !resolved.AlternativesResolved ||
            string.IsNullOrWhiteSpace(resolved.RoutingGeneration) ||
            (resolved.Primary ? resolved.TeamId != Uuid.Empty || resolved.RoleId != Uuid.Empty :
                resolved.TeamId == Uuid.Empty || resolved.RoleId == Uuid.Empty))
            throw new AccessAdmissionUnavailableException();

        var minimum = resolved.Primary ? 5 : 9;
        if (resolved.Participants is null)
            throw new AccessAdmissionUnavailableException();
        var count = resolved.Participants.Count;
        if (count < minimum || count > minimum + 3)
            throw new AccessAdmissionUnavailableException();
        var fixedPath = new AccessAdmissionStableRead.Participant[count];
        try
        {
            for (var index = 0; index < count; index++)
                fixedPath[index] = resolved.Participants[index];
        }
        catch (ArgumentOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        catch (IndexOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        // Revalidate the exact immutable route copied for the evaluator.
        resolved = resolved with { Participants = Array.AsReadOnly(fixedPath) };

        var evaluator = new AccessAdmissionSemanticEvaluator(new AccessAdmissionStableRead(source, clock),
            supportedPolicyGeneration, supportedPolicyContentSha256);
        var candidate = new AccessAdmissionSemanticEvaluator.Candidate(credential, programId, target, intentSha256, fixedPath);
        var decision = await evaluator.EvaluateProgramCreationAsync(
            new AccessAdmissionSemanticEvaluator.ProgramCandidate(candidate, tenantId, resolved.TeamId, resolved.RoleId, resolved.Primary), ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!credentials.IsQualified || !programRoutes.IsQualified || !source.IsFreshnessQualified ||
            !await programRoutes.IsCurrentAsync(resolved, ct).ConfigureAwait(false) ||
            !credentials.IsQualified || !programRoutes.IsQualified || !source.IsFreshnessQualified)
            throw new AccessAdmissionUnavailableException();
        return decision.Status switch
        {
            AccessAdmissionSemanticEvaluator.Status.Allowed when decision.Evidence is not null => decision.Evidence,
            AccessAdmissionSemanticEvaluator.Status.Denied => null,
            _ => throw new AccessAdmissionUnavailableException(),
        };
    }

    public async ValueTask<AccessAdmissionEvidence?> AdmitProgramSelectionAsync(IRequestContext context,
        Uuid tenantId, Uuid programId, Uuid operationId, string target, string intentSha256,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var actor = context.Actor;
        ct.ThrowIfCancellationRequested();
        if (credentials is null || selectionRoutes is null || source is null || !credentials.IsQualified ||
            !selectionRoutes.IsQualified || !source.IsFreshnessQualified || tenantId == Uuid.Empty ||
            programId == Uuid.Empty || operationId == Uuid.Empty || !CanonicalStream(target) ||
            target != new ComplianceProgram(tenantId, programId).Stream.ToString() || !Digest(intentSha256))
            throw new AccessAdmissionUnavailableException();

        var credential = await credentials.ResolveAsync(context, ct).ConfigureAwait(false);
        if (credential is null || !MatchesPrincipal(actor, credential))
            throw new AccessAdmissionUnavailableException();
        var resolved = await selectionRoutes.ResolveAsync(credential, tenantId, programId, ct).ConfigureAwait(false);
        if (resolved is null || !credentials.IsQualified || !selectionRoutes.IsQualified || !source.IsFreshnessQualified ||
            resolved.TenantId != tenantId || resolved.ProgramId != programId || resolved.UserId != credential.UserId ||
            !resolved.AlternativesResolved || string.IsNullOrWhiteSpace(resolved.RoutingGeneration) ||
            (resolved.Primary ? resolved.TeamId != Uuid.Empty || resolved.RoleId != Uuid.Empty || resolved.Scope is not null :
                resolved.TeamId == Uuid.Empty || resolved.RoleId == Uuid.Empty || resolved.Scope is not { } scope ||
                scope.ResourceType is not null ||
                !(scope.Kind == AccessGrantScopeKind.Organization && scope.Id == tenantId ||
                  scope.Kind == AccessGrantScopeKind.Program && scope.Id == programId)))
            throw new AccessAdmissionUnavailableException();

        var minimum = resolved.Primary ? 5 : 9;
        if (resolved.Participants is null)
            throw new AccessAdmissionUnavailableException();
        var count = resolved.Participants.Count;
        if (count < minimum || count > minimum + 3)
            throw new AccessAdmissionUnavailableException();
        var fixedPath = new AccessAdmissionStableRead.Participant[count];
        try
        {
            for (var index = 0; index < count; index++)
                fixedPath[index] = resolved.Participants[index];
        }
        catch (ArgumentOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        catch (IndexOutOfRangeException) { throw new AccessAdmissionUnavailableException(); }
        resolved = resolved with { Participants = Array.AsReadOnly(fixedPath) };

        var evaluator = new AccessAdmissionSemanticEvaluator(new AccessAdmissionStableRead(source, clock),
            supportedPolicyGeneration, supportedPolicyContentSha256);
        var candidate = new AccessAdmissionSemanticEvaluator.Candidate(credential, operationId, target, intentSha256, fixedPath);
        var decision = await evaluator.EvaluateProgramSelectionAsync(
            new AccessAdmissionSemanticEvaluator.ProgramCandidate(candidate, tenantId, resolved.TeamId, resolved.RoleId,
                resolved.Primary, programId, resolved.Scope), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!credentials.IsQualified || !selectionRoutes.IsQualified || !source.IsFreshnessQualified ||
            !await selectionRoutes.IsCurrentAsync(resolved, ct).ConfigureAwait(false) ||
            !credentials.IsQualified || !selectionRoutes.IsQualified || !source.IsFreshnessQualified)
            throw new AccessAdmissionUnavailableException();
        return decision.Status switch
        {
            AccessAdmissionSemanticEvaluator.Status.Allowed when decision.Evidence is not null => decision.Evidence,
            AccessAdmissionSemanticEvaluator.Status.Denied => null,
            _ => throw new AccessAdmissionUnavailableException(),
        };
    }

    static bool MatchesPrincipal(ClaimsPrincipal actor, AccessAdmissionSemanticEvaluator.Credential credential)
    {
        if (credential.UserId == Uuid.Empty || credential.IdentityId == Uuid.Empty ||
            credential.NotBefore == default || credential.ExpiresAt == default || credential.NotBefore >= credential.ExpiresAt ||
            RequestActor.IsSystem(actor) || !UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId) ||
            userId != credential.UserId)
            return false;
        var identity = actor.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated &&
            string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        return identity is not null &&
            string.Equals(identity.FindFirst("sub")?.Value, credential.Subject, StringComparison.Ordinal) &&
            string.Equals(identity.FindFirst("iss")?.Value, credential.Issuer, StringComparison.Ordinal) &&
            (credential.Scheme == AccessAdmissionSemanticEvaluator.CredentialScheme.FullParticipants
                ? credential.SessionId != Uuid.Empty
                : credential.Scheme == AccessAdmissionSemanticEvaluator.CredentialScheme.BdgrzIdentityOnly &&
                  credential.ExactIdentityBindingCertified &&
                  Uuid.TryParse(identity.FindFirst("user_identity_id")?.Value, CultureInfo.InvariantCulture, out var identityId) &&
                  identityId == credential.IdentityId);
    }

    static bool Digest(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool CanonicalStream(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        { return string.Equals(EventStreamAddress.Parse(value).ToString(), value, StringComparison.Ordinal); }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
    }
}
