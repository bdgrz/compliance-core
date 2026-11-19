using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Examinations.Services;
namespace Bdgrz.Compliance.Features.FirmServices.Readers;

/// <summary>Source-backed bridge for canonical examinations while engagement storage is cut over.</summary>
sealed class ExaminationAttestEngagementReader(ServiceEngagementViews engagements,
    FirmStaffViews staff, NonattestServiceRecordReader records, IAggregateReader reader,
    IDomainEventReader events, IImmutableContentStore content, TimeProvider clock)
    : IExaminationAttestEngagementReader
{
    public async ValueTask<Result> RequireAcceptedOpenAttestAsync(Uuid tenantId, Uuid engagementId,
        CancellationToken ct)
    {
        var view = await engagements.GetAsync(tenantId, engagementId, ct).ConfigureAwait(false);
        return IsAcceptedOpenAttest(view, tenantId, engagementId) ? Result.Success
            : Refuse(RequestErrorKind.Conflict, "The examination needs an accepted, open attest engagement in its tenant.");
    }

    public async ValueTask<Result> RequireAssignedStaffAsync(Uuid tenantId, Uuid engagementId, Uuid userId,
        CancellationToken ct)
    {
        var accepted = await FindAcceptedAsync(tenantId, engagementId, ct).ConfigureAwait(false);
        if (accepted is null || userId == Uuid.Empty ||
            await staff.FindByUserAsync(userId, ct).ConfigureAwait(false) is not { IsActive: true, Practice: "attest" } current)
            return Refuse(RequestErrorKind.Forbidden, "Current active attest staff assignment is required.");
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        return !accepted.DeniedByReceipt.Any(value => value.UserId == userId && value.StaffMemberId == current.StaffMemberId) &&
            accepted.Acceptance.Assignments.Any(value => value.IsCurrent && value.UserId == userId &&
                value.StaffMemberId == current.StaffMemberId && value.Practice == "attest" &&
                value.AssignedAt <= now && (value.EffectiveFrom == default || value.EffectiveFrom <= today))
            ? Result.Success
            : Refuse(RequestErrorKind.Forbidden, "Current active attest staff assignment is required.");
    }

    public async ValueTask<Result> RequireUnimpairedAsync(Uuid tenantId, Uuid engagementId,
        DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var accepted = await FindAcceptedAsync(tenantId, engagementId, ct).ConfigureAwait(false);
        if (accepted is null || periodStart == default || periodEnd < periodStart)
            return Refuse(RequestErrorKind.Conflict, "A current attest engagement and exact examination period are required.");
        var rules = accepted.Acceptance.Rules;
        if (!rules.IsRatified || rules.Content.LookBackMonths is <= 0 or > 1200)
            return Refuse(RequestErrorKind.Conflict, "The accepted engagement has no valid ratified rule-set version.");
        DateOnly windowStart;
        try
        {
            windowStart = periodStart.AddMonths(-rules.Content.LookBackMonths);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Refuse(RequestErrorKind.Validation, "The examination look-back is outside the supported date range.");
        }

        var legacyOwner = await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
        var legacy = await ClientServiceHistory.ReadAsync(events, content, legacyOwner, ct).ConfigureAwait(false);
        var current = await records.ReadAllAsync(tenantId, ct).ConfigureAwait(false);
        if (legacy.Select(static value => value.ServiceRecordId).Intersect(current.Select(static value => value.RecordId)).Any())
            return Refuse(RequestErrorKind.Conflict, "A service identity occurs in both source generations; convert it explicitly.");
        foreach (var service in legacy)
        {
            var terms = service.Content;
            if (!Overlaps(terms.StartedOn, terms.EndedOn, windowStart, periodEnd))
                continue;
            var classifications = rules.Content.ServiceRules.Where(value => value.ServiceType == terms.ServiceType).ToArray();
            if (classifications.Length != 1)
                return Refuse(RequestErrorKind.Conflict, "The engagement's rule set does not classify a current service.");
            var classification = terms.InvolvedManagementFunctions
                ? classifications[0].ManagementFunctionsClassification : classifications[0].Classification;
            if (classification == "impairing")
                return Impaired();
            if (classification is not ("compatible" or "conditionally_compatible"))
                return Refuse(RequestErrorKind.Conflict, "A current service classification is invalid.");
        }
        foreach (var service in current.Where(static value => !value.IsVoided))
        {
            if (!Overlaps(service.Terms.StartedOn, service.Terms.EndedOn, windowStart, periodEnd))
                continue;
            var classifications = rules.Content.ServiceRules.Where(value => value.ServiceType == service.Terms.ServiceKind).ToArray();
            if (classifications.Length != 1)
                return Refuse(RequestErrorKind.Conflict, "The engagement's rule set does not classify a current service.");
            if (classifications[0].Classification == "impairing")
                return Impaired();
            if (classifications[0].Classification is not ("compatible" or "conditionally_compatible"))
                return Refuse(RequestErrorKind.Conflict, "A current service classification is invalid.");
        }
        return Result.Success;
    }

    async ValueTask<AcceptedEngagement?> FindAcceptedAsync(Uuid tenantId, Uuid engagementId, CancellationToken ct)
    {
        if (tenantId == Uuid.Empty || engagementId == Uuid.Empty)
            return null;
        var accepted = (await engagements.AcceptedAsync(tenantId, ct).ConfigureAwait(false))
            .SingleOrDefault(value => value.Engagement.EngagementId == engagementId);
        return accepted is not null && IsAcceptedOpenAttest(accepted.Engagement, tenantId, engagementId) &&
            accepted.Acceptance.Status == "active" ? accepted : null;
    }

    static bool IsAcceptedOpenAttest(ServiceEngagementView? view, Uuid tenantId, Uuid engagementId) =>
        tenantId != Uuid.Empty && engagementId != Uuid.Empty && view is not null && view.TenantId == tenantId &&
        view.EngagementId == engagementId && view.Content.Practice == "attest" && view.Status == "accepted";

    static bool Overlaps(DateOnly startedOn, DateOnly? endedOn, DateOnly windowStart, DateOnly periodEnd) =>
        startedOn <= periodEnd && (endedOn is null || endedOn >= windowStart);
    static Result Impaired() => Refuse(RequestErrorKind.Conflict, "The attest engagement has an independence_impaired gap.");
    static Result Refuse(RequestErrorKind kind, string message) => Result.Failure(new(kind, message));
}
