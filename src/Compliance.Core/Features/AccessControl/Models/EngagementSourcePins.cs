using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     Pins the exact sources an engagement operation relies on, each as a <see cref="SourceUseRequirement" /> that its
///     own use receiver holds before the operation is authorized. A pin that no longer matches the evidence the caller
///     verified means a source changed while the operation was prepared, and the caller reloads and retries.
/// </summary>
sealed class EngagementSourcePins(IAggregateReader reader, IImmutableContentStore content,
    IndependenceRuleCatalogReader ruleSources, IndependenceRuleRatificationReader ratificationSources)
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
        var catalog = (await ruleSources.ReadAsync(ct).ConfigureAwait(false)).Owner;
        var currentRatification = await ratificationSources.ReadAsync(ct).ConfigureAwait(false);
        var ratifications = currentRatification.Owner;
        var ratification = currentRatification.Active;
        if (catalog.LatestRevision is not { } latestRules || ratifications.LatestRatification is not { } latestRatification ||
            ratification is null || ratification.RuleVersion != rules.Version || rules.Ratification != ratification)
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
