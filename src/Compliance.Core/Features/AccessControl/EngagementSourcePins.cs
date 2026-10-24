using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Pins the exact sources an engagement operation relies on, each as a <see cref="SourceUseRequirement" /> that its
///     own use receiver holds before the operation is authorized. A pin that no longer matches the evidence the caller
///     verified means a source changed while the operation was prepared, and the caller reloads and retries.
/// </summary>
sealed class EngagementSourcePins(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>The pin of one staff record at the exact revision the caller verified, or null when it moved.</summary>
    public async ValueTask<SourceUseRequirement?> StaffAsync(Uuid staffMemberId, long expectedRevision,
        CancellationToken ct)
    {
        var staff = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId), ct).ConfigureAwait(false);
        return staff.Latest is { } latest && (long)latest.Revision == expectedRevision
            ? new SourceUseRequirement(staff.Stream, NativeFactReference.Of(staff.Stream, latest), latest.Revision,
                SourceUsePurpose.EngagementPersonalDecision, latest.StaffContent)
            : null;
    }

    /// <summary>The pins of the partner's staff record and engagement-partner designation, or null when either moved.</summary>
    public async ValueTask<IReadOnlyList<SourceUseRequirement>?> PartnerAsync(Uuid tenantId, Uuid userId,
        CurrentProfessionalDuty duty, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(duty);
        var staff = await StaffAsync(duty.Staff.StaffMemberId, duty.Staff.Revision, ct).ConfigureAwait(false);
        var designation = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(userId,
            FirmProfessionalDuty.EngagementPartner, tenantId), ct).ConfigureAwait(false);
        if (staff is null || designation.Latest is not DomainEvent latestDuty ||
            designation.LatestContent is not { } dutyContent ||
            (long)designation.Revision != duty.Designation.Revision)
            return null;
        return
        [
            staff,
            new SourceUseRequirement(designation.Stream, NativeFactReference.Of(designation.Stream, latestDuty),
                designation.Revision, SourceUsePurpose.EngagementPersonalDecision, dutyContent),
        ];
    }

    /// <summary>The pins of the draft rule catalog and the ratification catalog of the active ratified rules, or null when they moved.</summary>
    public async ValueTask<IReadOnlyList<SourceUseRequirement>?> RulesAsync(IndependenceRuleVersionView rules,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var catalog = await reader.HydrateAsync(new IndependenceRuleCatalog(), ct).ConfigureAwait(false);
        var ratifications = await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(), ct)
            .ConfigureAwait(false);
        if (catalog.LatestRevision is not { } latestRules || ratifications.LatestRatification is not { } latestRatification ||
            ratifications.ForVersion(rules.Version) is not { } ratification || rules.Ratification != ratification)
            return null;
        var rulesContent = await content.WriteJsonAsync(EngagementPersonalDecisionEffects.PlatformRealm,
            Uuid.CreateVersion4(), EngagementPersonalDecisionEffects.RatifiedRulesSchema,
            IndependenceRecordValidation.Freeze(rules), ct).ConfigureAwait(false);
        var ratificationContent = await content.WriteJsonAsync(EngagementPersonalDecisionEffects.PlatformRealm,
            Uuid.CreateVersion4(), EngagementPersonalDecisionEffects.RatificationSchema, ratification, ct)
            .ConfigureAwait(false);
        return
        [
            new SourceUseRequirement(catalog.Stream, NativeFactReference.Of(catalog.Stream, latestRules),
                (ulong)catalog.Sequence, SourceUsePurpose.EngagementPersonalDecision, rulesContent),
            new SourceUseRequirement(ratifications.Stream, NativeFactReference.Of(ratifications.Stream, latestRatification),
                (ulong)ratifications.Sequence, SourceUsePurpose.EngagementPersonalDecision, ratificationContent),
        ];
    }
}
