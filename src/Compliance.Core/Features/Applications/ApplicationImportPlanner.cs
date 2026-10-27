using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Builds the complete plan of one batch: every planned row, in authored order with retirement rows after them in
///     their proposal order, and the canonical target groups the rows fold into. It is a pure function of the staged
///     rows, the correlations, the source claims, the sealed omission proposal, the current retirement impact and the
///     current targets, so acceptance freezes exactly what it verified. Nothing is capped by a count.
/// </summary>
static class ApplicationImportPlanner
{
    public static Result<IReadOnlyList<ApplicationImportPlannedRow>> PlanRows(ApplicationImportBatch batch, ApplicationImportBatchHistorySnapshot history,
        IReadOnlyList<ApplicationImportStagedRow> staged, ApplicationImportSourceView source,
        ApplicationImportOmissionProposalContent? proposal, string? proposalSha256,
        IReadOnlyDictionary<Uuid, ApplicationChangePreview>? retirementImpacts)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(source);
        if (!history.Matches(batch))
            return Fail(RequestErrorKind.Conflict, "The complete row choices belong to another prepared batch cut.");
        var tenantId = Uuid.Parse(batch.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var missing = source.Missing(batch, staged);
        var retirementRows = new List<ApplicationImportPlannedRow>();
        if (missing.Count > 0)
        {
            if (batch.Coverage != "declared_complete" || proposal is null || proposalSha256 is null ||
                proposal.Rows.Count != missing.Count || retirementImpacts is null ||
                retirementImpacts.Count != proposal.Rows.Count)
                return Fail(RequestErrorKind.Conflict,
                    "Missing source claims require a current sealed retirement proposal and complete impact evidence.");
            // The sealed proposal must name exactly the claims this complete source no longer observes.
            if (proposal.Rows.Any(retirement => !missing.Any(claim =>
                    claim.Observation.SourceRecordId == retirement.SourceRecordId &&
                    claim.Observation.ApplicationId == retirement.ApplicationId &&
                    (claim.Observation.ExpectedApplicationRevision ?? 1) == retirement.ExpectedApplicationRevision)))
                return Fail(RequestErrorKind.Conflict,
                    "The sealed retirement proposal does not match the source's current omissions.");
            foreach (var retirement in proposal.Rows)
            {
                if (!retirementImpacts.TryGetValue(retirement.ApplicationId, out var impact) ||
                    !ApplicationImportRetirementImpact.IsUsable(impact, tenantId, retirement))
                    return Fail(RequestErrorKind.Conflict,
                        "Retirement requires complete current impact evidence with no known dependent records.");
                retirementRows.Add(new ApplicationImportPlannedRow(
                    ApplicationImportEffects.RetirementRowId(batch.Id, retirement.SourceClaimId),
                    retirement.SourceRecordId, "retire", retirement.ApplicationId,
                    retirement.ExpectedApplicationRevision, string.Empty, string.Empty, null)
                {
                    RetirementSourceClaimId = retirement.SourceClaimId,
                    RetirementProposalSha256 = proposalSha256,
                    RetirementImpactDigest = impact.ImpactDigest,
                    RetirementReason = proposal.Reason,
                });
            }
        }
        else if (retirementImpacts is { Count: > 0 })
        {
            return Fail(RequestErrorKind.Conflict, "Retirement impact evidence does not match this source snapshot.");
        }

        var planned = new List<ApplicationImportPlannedRow>(staged.Count + retirementRows.Count);
        foreach (var row in staged)
        {
            var choice = history.GetCorrelation(row.RowId);
            var claim = row.SourceRecordId is not null ? source.Find(row.SourceRecordId) : null;
            if (row.ValidationFindings.Count > 0 || (choice is null && claim is null))
                return Fail(RequestErrorKind.Conflict, "Every import row must be valid and explicitly correlated.");
            var targetId = choice?.ApplicationId ?? claim!.Observation.ApplicationId;
            if (claim is not null && (targetId != claim.Observation.ApplicationId || choice?.Decision == "create_new"))
                return Fail(RequestErrorKind.Conflict,
                    "A committed source claim cannot be rebound to another application.");
            planned.Add(new ApplicationImportPlannedRow(row.RowId, row.SourceRecordId!,
                choice?.Decision ?? "link_existing", targetId,
                choice is not null ? choice.ExpectedApplicationRevision
                    : claim!.Observation.ExpectedApplicationRevision ?? 1,
                row.Name!, row.Purpose!, row.OwnerReference));
        }

        planned.AddRange(retirementRows);
        return Result<IReadOnlyList<ApplicationImportPlannedRow>>.Success(planned.AsReadOnly());
    }

    /// <summary>
    ///     Folds the rows into canonical target groups against the current targets. The first row of a group fixes its
    ///     baseline, which is compared once; every later row of the group is compared against the candidate the
    ///     earlier rows produce.
    /// </summary>
    public static Result<IReadOnlyList<ApplicationImportTargetDraft>> PlanGroups(Uuid tenantId,
        IReadOnlyList<ApplicationImportPlannedRow> rows, IReadOnlyDictionary<Uuid, DeclaredApplication> targets)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(targets);
        var groups = new List<ApplicationImportTargetDraft>();
        foreach (var group in rows.GroupBy(row => row.ApplicationId)
                     .OrderBy(group => ApplicationImportEffects.TargetKey(group.Key), StreamKeyOrder.Instance))
        {
            if (!targets.TryGetValue(group.Key, out var target) || target.Id != group.Key ||
                target.Stream.Realm != tenantId.ToString())
                return GroupFail(RequestErrorKind.NotFound, "The linked application was not found.");
            var groupRows = group.ToArray();
            var first = groupRows[0];
            if (first.Decision is "link_existing" or "retire")
            {
                if (!target.IsCreated)
                    return GroupFail(RequestErrorKind.NotFound, "The linked application was not found.");
                if (first.Decision == "retire" && target.CheckPendingImportChanges() is { } unsettled)
                    return Result<IReadOnlyList<ApplicationImportTargetDraft>>.Failure(unsettled);
                if (target.IsRetired)
                    return GroupFail(RequestErrorKind.Conflict, first.Decision == "retire"
                        ? "The retirement target is already retired."
                        : "The linked application is retired.");
                if (target.Revision != first.ExpectedApplicationRevision)
                    return Result<IReadOnlyList<ApplicationImportTargetDraft>>.Failure(
                        VersionedRecordRules.StaleRevision("application", target.Revision).ToRequestError());
            }
            else if (target.IsCreated || target.Revision != 0)
            {
                return GroupFail(RequestErrorKind.Conflict, "The new import target already exists.");
            }

            var fold = ApplicationImportGroupFold.Fold(target.IsCreated, target.IsRetired, target.Revision, groupRows);
            if (!fold.IsSuccess)
                return Result<IReadOnlyList<ApplicationImportTargetDraft>>.Failure(fold.Error!);
            var created = groupRows.FirstOrDefault(row => row.Decision == "create_new");
            var retired = groupRows.FirstOrDefault(row => row.Decision == "retire");
            var candidate = new ApplicationImportCandidateContent(group.Key, created is not null, retired is not null,
                created?.Name.Trim(), created?.Purpose.Trim(), Normalize(created?.OwnerReference),
                retired?.RetirementReason, (ulong)fold.Value![^1]);
            var proofs = groupRows.Where(row => row.Decision == "retire").Select(row =>
                new ApplicationImportRetirementProof(row.RowId, row.RetirementSourceClaimId!.Value,
                    row.RetirementProposalSha256!, row.RetirementImpactDigest!)).ToArray();
            groups.Add(new ApplicationImportTargetDraft(group.Key, (ulong)target.Revision, groupRows, fold.Value!,
                candidate, proofs));
        }

        return Result<IReadOnlyList<ApplicationImportTargetDraft>>.Success(groups.AsReadOnly());
    }

    static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static Result<IReadOnlyList<ApplicationImportPlannedRow>> Fail(RequestErrorKind kind, string message) =>
        Result<IReadOnlyList<ApplicationImportPlannedRow>>.Failure(new RequestError(kind, message));

    static Result<IReadOnlyList<ApplicationImportTargetDraft>> GroupFail(RequestErrorKind kind, string message) =>
        Result<IReadOnlyList<ApplicationImportTargetDraft>>.Failure(new RequestError(kind, message));
}
