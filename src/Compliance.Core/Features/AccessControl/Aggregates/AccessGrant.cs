using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

public sealed class AccessGrant : Aggregate
{
    readonly Uuid _tenantId;
    AccessGrantTerms? _terms;
    Uuid? _membershipEpisodeId;
    bool _isRevoked;
    Uuid _issuedEventId;

    public bool IsRevoked => _isRevoked;
    public Uuid? MembershipEpisodeId => _membershipEpisodeId;

    public AccessGrant(Uuid tenantId, Uuid grantId)
        : base(grantId, new EventStreamAddress(tenantId.ToString(), "access-grants", grantId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessGrantIssued>(ev =>
        {
            _terms = ev.Terms;
            _membershipEpisodeId = ev.MembershipEpisodeId;
            _issuedEventId = ev.Metadata.EventId;
        });
        On<AccessGrantRevoked>(_ => _isRevoked = true);
    }

    public Result Issue(AccessGrantTerms terms, Uuid? membershipEpisodeId = null)
    {
        var validation = Validate(terms);
        if (validation is not null)
            return Result.Failure(validation);

        if (ReplayIssue(new AccessGrantProposal(terms.Principal, terms.RoleId, terms.Scope,
                terms.Source, terms.EffectiveFrom, terms.EffectiveUntil), terms.GrantedBy) is { } retry)
            return retry;

        RaiseEvent(new AccessGrantIssued(_tenantId, Id, terms, membershipEpisodeId));
        return Result.Success;
    }

    /// <summary>
    /// Resolves the immutable creation receipt without consulting current grant, member, role or source state.
    /// A successful replay never reissues a revoked grant or rebinds its original membership episode.
    /// </summary>
    public Result? ReplayIssue(AccessGrantProposal? proposal, ActorReference actor)
    {
        if (_terms is not { } original)
            return null;
        var authored = new AccessGrantProposal(original.Principal, original.RoleId, original.Scope,
            original.Source, original.EffectiveFrom, original.EffectiveUntil);
        return proposal == authored && actor.Kind == original.GrantedBy.Kind && actor.Id == original.GrantedBy.Id
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The grant identity already accepted another proposal or original actor."));
    }

    public Result Revoke(ActorReference revokedBy, DateTimeOffset revokedAt)
    {
        if (_terms is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The access grant was not found."));
        if (_isRevoked)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The access grant is already revoked."));
        if (!IsValidActor(revokedBy) || revokedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A grant revocation requires an actor and timestamp."));

        RaiseEvent(new AccessGrantRevoked(_tenantId, Id, revokedBy, revokedAt));
        return Result.Success;
    }

    internal Result RevokeForRetirement(MembershipTerminationProof proof, Uuid issuedEventId,
        DateTimeOffset revokedAt)
    {
        if (proof.TenantId != _tenantId || proof.MembershipEpisodeId == Uuid.Empty || proof.EventId == Uuid.Empty ||
            issuedEventId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Exact retirement and grant proofs are required."));
        if (_terms is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The proven grant is not available yet.", isTransient: true));
        if (_isRevoked || _issuedEventId != issuedEventId || _membershipEpisodeId != proof.MembershipEpisodeId ||
            _terms.Principal.Kind != AccessGrantPrincipalKind.Member || _terms.Principal.Id != proof.MemberId)
            return Result.Success;
        return Revoke(ActorReference.ForMember(proof.ActorMemberId, proof.ActorDisplay), revokedAt);
    }

    RequestError? Validate(AccessGrantTerms terms)
    {
        if (terms is null || terms.Principal is null || terms.Scope is null || terms.Source is null)
            return new RequestError(RequestErrorKind.Validation, "An access grant requires complete terms.");
        if (terms.Principal.Id == Uuid.Empty || !Enum.IsDefined(terms.Principal.Kind) ||
            terms.RoleId == Uuid.Empty || terms.Scope.Id == Uuid.Empty ||
            !Enum.IsDefined(terms.Scope.Kind) || !IsValidActor(terms.GrantedBy) ||
            string.IsNullOrWhiteSpace(terms.Source.Kind) || string.IsNullOrWhiteSpace(terms.Source.Id) ||
            terms.EffectiveFrom == default ||
            terms.EffectiveUntil is { } until && until <= terms.EffectiveFrom)
            return new RequestError(RequestErrorKind.Validation, "The access grant terms are invalid.");
        if (terms.Scope.Kind == AccessGrantScopeKind.Organization && terms.Scope.Id != _tenantId)
            return new RequestError(RequestErrorKind.Validation, "An organization scope requires an organization id.");
        if (terms.Scope.Kind == AccessGrantScopeKind.SharedResource &&
            string.IsNullOrWhiteSpace(terms.Scope.ResourceType))
            return new RequestError(RequestErrorKind.Validation,
                "A shared resource scope requires its resource type.");
        if (terms.Scope.Kind != AccessGrantScopeKind.SharedResource && terms.Scope.ResourceType is not null)
            return new RequestError(RequestErrorKind.Validation,
                "Only a shared resource scope may specify a resource type.");
        return null;
    }

    static bool IsValidActor(ActorReference? actor) =>
        actor is { Kind: "member" } &&
        !string.IsNullOrWhiteSpace(actor.Id) && !string.IsNullOrWhiteSpace(actor.Display);
}
