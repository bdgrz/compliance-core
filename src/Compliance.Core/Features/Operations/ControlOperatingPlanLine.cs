using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The plan versions of one control and their current states, folded from the facts of a
///     <see cref="ControlOperatingPlan" /> and the immutable content they reference. Approval supersedes the earlier
///     approved version and closes its effective interval at the new version's start.
/// </summary>
public sealed class ControlOperatingPlanLine
{
    readonly List<ControlOperatingPlanView> _plans = [];
    readonly Dictionary<Uuid, ControlOperatingPlanApproved> _approvals = [];

    ControlOperatingPlanLine(Uuid controlId) => ControlId = controlId;

    /// <summary>The control this line plans.</summary>
    public Uuid ControlId { get; }

    /// <summary>The logical revision of the line when it was read.</summary>
    public long Revision { get; private set; }

    /// <summary>Every plan version in proposal order.</summary>
    public IReadOnlyList<ControlOperatingPlanView> Plans => _plans;

    /// <summary>The approval fact of each approved plan version, by plan version id.</summary>
    public IReadOnlyDictionary<Uuid, ControlOperatingPlanApproved> Approvals => _approvals;

    /// <summary>The line of a control that has no plan yet.</summary>
    public static ControlOperatingPlanLine Empty(Uuid controlId) => new(controlId);

    /// <summary>Folds the facts of a hydrated plan line, reading each fact's immutable content.</summary>
    public static async Task<ControlOperatingPlanLine> ReadAsync(ControlOperatingPlan plan,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(content);
        if (tenantId != plan.TenantId)
            throw new ImmutableContentIntegrityException("The operating composition belongs to another tenant.");
        var line = new ControlOperatingPlanLine(plan.ControlId) { Revision = plan.Revision };
        foreach (var fact in await ControlOperatingPlanHistory.ReadAsync(events, plan, ct).ConfigureAwait(false))
            switch (fact)
            {
                case ControlOperatingPlanProposed proposed:
                    var body = await ControlOperatingContent.ReadAsync<ControlOperatingPlanView>(content,
                        tenantId, proposed.PlanContent, ct).ConfigureAwait(false);
                    if (proposed.PlanContent.Schema != ControlOperatingContent.PlanSchema || body.TenantId != tenantId ||
                        body.ProgramId != plan.ProgramId || body.ControlId != plan.ControlId ||
                        body.PlanVersionId != proposed.Operation.LogicalId || body.Revision != proposed.Revision ||
                        body.Status != ControlOperationStates.PendingApproval || body.ProposedBy != proposed.Operation.Actor ||
                        body.ProposedAt != proposed.Operation.AcceptedAt || body.ProposerMemberId.ToString() != proposed.Operation.Actor.Id ||
                        body.ExpectedEvidence is null || line._plans.Any(value => value.PlanVersionId == body.PlanVersionId))
                        throw new ImmutableContentIntegrityException("The immutable operating plan differs from its native source scope, identity or original attribution.");
                    line._plans.Add(body);
                    break;
                case ControlOperatingPlanApproved approved:
                    if (approved.ReassignmentsManifest.Schema != ControlOperatingContent.ReassignmentsSchema ||
                        line.Pending?.PlanVersionId != approved.PlanVersionId || approved.ApprovedBy != approved.Operation.Actor ||
                        approved.ApproverMemberId.ToString() != approved.Operation.Actor.Id)
                        throw new ImmutableContentIntegrityException("The operating approval differs from its exact pending native version or actor.");
                    line.Approve(approved, await ControlOperatingContent
                        .ReadAsync<IReadOnlyList<OccurrenceReassignmentView>>(content, tenantId,
                            approved.ReassignmentsManifest, ct).ConfigureAwait(false));
                    break;
            }

        return line;
    }

    /// <summary>The latest approved plan version.</summary>
    public ControlOperatingPlanView? Current =>
        _plans.LastOrDefault(static plan => plan.Status == ControlOperationStates.Approved);

    /// <summary>The plan version awaiting approval.</summary>
    public ControlOperatingPlanView? Pending =>
        _plans.LastOrDefault(static plan => plan.Status == ControlOperationStates.PendingApproval);

    /// <summary>One plan version.</summary>
    public ControlOperatingPlanView? Find(Uuid planVersionId) =>
        _plans.Find(plan => plan.PlanVersionId == planVersionId);

    /// <summary>The approved or superseded plan whose effective interval contains the date.</summary>
    public ControlOperatingPlanView? PlanOn(DateOnly date) => _plans.LastOrDefault(plan =>
        plan.Status is ControlOperationStates.Approved or ControlOperationStates.Superseded &&
        plan.EffectiveFrom <= date && (plan.EffectiveUntil is null || date < plan.EffectiveUntil));

    /// <summary>The current, pending and historical versions as the plan set view.</summary>
    public ControlOperatingPlanSetView ToSetView() => new(ControlId, Revision, Current, Pending, _plans.ToArray());

    void Approve(ControlOperatingPlanApproved approved, IReadOnlyList<OccurrenceReassignmentView> reassignments)
    {
        for (var index = 0; index < _plans.Count; index++)
        {
            var plan = _plans[index];
            if (plan.Status == ControlOperationStates.Approved && plan.PlanVersionId != approved.PlanVersionId)
                _plans[index] = plan with { Status = ControlOperationStates.Superseded };
        }

        var pendingIndex = _plans.FindIndex(plan => plan.PlanVersionId == approved.PlanVersionId);
        var pending = _plans[pendingIndex];
        for (var index = 0; index < _plans.Count; index++)
        {
            var plan = _plans[index];
            if (plan.Status == ControlOperationStates.Superseded && plan.EffectiveUntil is null)
                _plans[index] = plan with { EffectiveUntil = pending.EffectiveFrom };
        }

        _plans[pendingIndex] = pending with
        {
            Status = ControlOperationStates.Approved,
            Revision = approved.Revision,
            ApprovedBy = approved.ApprovedBy,
            ApprovedAt = approved.ApprovedAt,
            ApprovalRationale = approved.Rationale,
            ApprovalSeparationOfDutiesWaiverId = approved.SeparationOfDutiesWaiverId,
            Reassignments = reassignments,
        };
        _approvals[approved.PlanVersionId] = approved;
    }
}
