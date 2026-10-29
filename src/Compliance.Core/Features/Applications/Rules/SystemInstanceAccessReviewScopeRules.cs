using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Rules;

/// <summary>
/// Pure rules and effective-dated history composition for one system instance
/// (M0-D05). A new decision supersedes; it never edits a prior one.
/// </summary>
public sealed class SystemInstanceAccessReviewScopeRules
{
    public const string Included = "included";
    public const string Excluded = "excluded";

    readonly Uuid _tenantId;
    readonly List<AccessReviewScopeDecisionView> _decisions = [];

    public IReadOnlyList<AccessReviewScopeDecisionView> Decisions => _decisions;

    public Uuid Id { get; }

    public SystemInstanceAccessReviewScopeRules(Uuid tenantId, Uuid systemInstanceId) =>
        (_tenantId, Id) = (tenantId, systemInstanceId);

    /// <summary>Returns the latest decision whose effective date is not after <paramref name="asOf"/>.</summary>
    public AccessReviewScopeDecisionView? EffectiveAt(DateTimeOffset asOf) =>
        AccessReviewScopeStatus.EffectiveAt(_decisions, asOf);

    public AccessReviewScopeView ToView(Uuid applicationId, DateTimeOffset asOf)
    {
        var effective = EffectiveAt(asOf);
        return new AccessReviewScopeView(_tenantId, applicationId, Id, asOf,
            effective?.Decision ?? AccessReviewScopeStatus.Unresolved, effective, [.. _decisions]);
    }

    public Result<AccessReviewScopeDecisionView> Decide(DeclaredSystemInstance instance,
        long expectedDecisionCount, Uuid decisionId, string decision, string reason,
        DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!instance.IsCreated)
            return Result<AccessReviewScopeDecisionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The system instance was not found."));
        return Decide(ScopedSystemInstance.From(instance), expectedDecisionCount, decisionId,
            decision, reason, effectiveFrom, reviewBy, approverMemberId, approverDisplay,
            decidedAt, waiver);
    }

    /// <summary>
    /// Decides scope for a registered or legacy application-stream instance at its exact revision.
    /// </summary>
    public Result<AccessReviewScopeDecisionView> Decide(ScopedSystemInstance instance,
        long expectedDecisionCount, Uuid decisionId, string decision, string reason,
        DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var retry = _decisions.FirstOrDefault(existing => existing.DecisionId == decisionId);
        if (retry is not null)
            return Result<AccessReviewScopeDecisionView>.Success(retry);
        var failure = Validate(instance, expectedDecisionCount, decision, reason, effectiveFrom,
            reviewBy, approverMemberId, decidedAt, waiver);
        if (failure is not null)
            return Result<AccessReviewScopeDecisionView>.Failure(failure);
        var normalizedDisplay = approverDisplay.Trim();
        var view = new AccessReviewScopeDecisionView(_tenantId, instance.ApplicationId, Id,
            instance.Revision, decisionId, _decisions.Count + 1, decision, reason.Trim(), effectiveFrom,
            reviewBy, ActorReference.ForMember(approverMemberId, normalizedDisplay), decidedAt, waiver?.Id);
        _decisions.Add(view);
        return Result<AccessReviewScopeDecisionView>.Success(view);
    }

    RequestError? Validate(ScopedSystemInstance instance, long expectedDecisionCount,
        string decision, string reason, DateTimeOffset effectiveFrom, DateTimeOffset? reviewBy,
        Uuid approverMemberId, DateTimeOffset decidedAt, SeparationOfDutiesWaiver? waiver)
    {
        if (instance.Id != Id)
            return new RequestError(RequestErrorKind.NotFound, "The system instance was not found.");
        if (instance.IsRetired)
            return new RequestError(RequestErrorKind.Conflict,
                "A retired system instance cannot receive a new scope decision.");
        if (decision is not (Included or Excluded))
            return new RequestError(RequestErrorKind.Validation,
                "The decision must be included or excluded.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return new RequestError(RequestErrorKind.Validation,
                "A scope decision requires a reason of at most 2000 characters.");
        if (reviewBy is { } review && review <= effectiveFrom)
            return new RequestError(RequestErrorKind.Validation,
                "The review date must be after the effective date.");
        if (expectedDecisionCount != _decisions.Count)
            return new RequestError(RequestErrorKind.Conflict,
                $"The instance has {_decisions.Count} scope decisions; expected {expectedDecisionCount}.");
        if (_decisions.Count > 0 && effectiveFrom < _decisions[^1].EffectiveFrom)
            return new RequestError(RequestErrorKind.Validation,
                "A new scope decision cannot take effect before the decision it supersedes.");

        // Separation of duties (M0-D03, M0-D05): the registrant and current access owner may
        // approve scope only with an independently approved exact-scope waiver.
        var conflict = approverMemberId == instance.RegisteredByMemberId ||
                       approverMemberId == instance.AccessOwnerMemberId;
        if (!conflict)
            return waiver is null ? null : new RequestError(RequestErrorKind.Forbidden,
                "A separation-of-duties waiver may only be used for a current conflict.");
        var scope = new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.SystemInstanceAccessReviewScope, Id, Id,
            instance.Revision, SeparationOfDutiesActions.Approve);
        return waiver is not null && waiver.TenantId == _tenantId &&
               waiver.Allows(scope, approverMemberId, decidedAt)
            ? null
            : new RequestError(RequestErrorKind.Forbidden,
                "The registrant or access owner cannot approve the system instance's access-review scope without an active exact-scope waiver.");
    }
}
