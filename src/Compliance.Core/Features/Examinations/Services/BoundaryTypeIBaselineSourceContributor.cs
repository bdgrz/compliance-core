using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>Resolves the exact approved boundary effective on the Type I examination date.</summary>
public sealed class BoundaryTypeIBaselineSourceContributor(
    IBoundaryDirectoryReader boundaries, ProgramSetupWorkReadConsistency consistency)
    : ITypeIBaselineSourceContributor
{
    public string Family => "approved_boundary";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(ExaminationEngagementView engagement,
        TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var sources = new List<TypeIBaselineSourceReference>(1);
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        var boundary = await boundaries.GetAsync(engagement.TenantId, engagement.BoundaryId, ct)
            .ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != engagement.TenantId ||
            boundary.ProgramId != engagement.ProgramId ||
            boundary.BoundaryId != engagement.BoundaryId)
        {
            blockers.Add("The engagement's approved boundary is unavailable in its program.");
        }
        else
        {
            var version = await boundaries.GetEffectiveVersionAsync(engagement.TenantId,
                engagement.BoundaryId, engagement.PeriodStart, ct).ConfigureAwait(false);
            if (version is null || version.TenantId != engagement.TenantId ||
                version.BoundaryId != engagement.BoundaryId || version.ProgramId != engagement.ProgramId ||
                version.Status != "approved" || version.EffectiveFrom is not { } effectiveFrom ||
                effectiveFrom > engagement.PeriodStart || version.VersionId == Uuid.Empty ||
                version.Revision < 1)
            {
                blockers.Add("No exact approved boundary version is effective on the Type I date.");
            }
            else
            {
                if (context.RecordedThrough is not null && !(await context.RequireReceipts(engagement.TenantId)
                        .ReadAsync(new EventStreamAddress(engagement.TenantId.ToString(), SystemBoundary.Area, version.BoundaryId.ToString()), ct)
                        .ConfigureAwait(false)).Includes(fact => fact is BoundaryApproved approved && approved.ChangeCount > 0 &&
                            approved.DraftVersionId == version.VersionId && approved.Revision == version.Revision))
                    return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The effective boundary changed after the frozen close receipt cutoff.", true));
                try
                {
                    sources.Add(new TypeIBaselineSourceReference("approved_boundary",
                        version.BoundaryId, version.VersionId, version.Revision,
                        SnapshotContentIdentity.ApprovedBoundaryVersion(version)));
                }
                catch (SnapshotContentException)
                {
                    blockers.Add("The effective boundary version has invalid canonical content.");
                }

                var boundaryCategories = version.Content.TrustServicesCategories.ToHashSet(
                    StringComparer.Ordinal);
                var engagementCategories = engagement.TrustServicesCategories.ToHashSet(
                    StringComparer.Ordinal);
                if (!boundaryCategories.SetEquals(engagementCategories))
                {
                    const string difference =
                        "The engagement categories do not match the approved boundary categories.";
                    scopeDifferences.Add(difference);
                    blockers.Add(difference);
                }
                if (!boundaryCategories.Contains("security"))
                    blockers.Add("The approved boundary does not include the required security category.");

                foreach (var entry in version.Content.Entries.Where(static entry => entry.Unresolved))
                {
                    var difference = $"Boundary scope entry {entry.EntryId} is unresolved.";
                    scopeDifferences.Add(difference);
                    blockers.Add(difference);
                }
            }
        }

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(engagement.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmed.Error);

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(), scopeDifferences.AsReadOnly()));
    }
}
