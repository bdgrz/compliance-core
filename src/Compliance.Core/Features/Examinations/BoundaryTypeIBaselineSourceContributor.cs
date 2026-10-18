using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Resolves the exact approved boundary effective on the Type I examination date.</summary>
public sealed class BoundaryTypeIBaselineSourceContributor(
    IBoundaryDirectoryReader boundaries, ProgramSetupWorkReadConsistency consistency)
    : ITypeIBaselineSourceContributor
{
    public string Family => "approved_boundary";

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct)
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
