using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.History;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Aggregates;

/// <summary>Bounded engagement authority. No complete terms, source lists or baseline history are retained.</summary>
public sealed class ExaminationEngagement : Aggregate
{
    public const string Area = "examinations";
    readonly Uuid _tenant;
    readonly Uuid _program;
    readonly Uuid _selectedBaseline;
    ImmutableContentRef? _selectedBaselineContent;
    public ExaminationEngagement(Uuid tenant, Uuid program, Uuid examination, Uuid selectedCommandId = default,
        string? selectedCommandKind = null, Uuid selectedBaselineId = default)
        : base(examination, new EventStreamAddress(tenant.ToString(), Area, examination.ToString()))
    {
        (_tenant, _program, _selectedBaseline) = (tenant, program, selectedBaselineId);
        Acceptance = new NativeAcceptance(selectedCommandId, selectedCommandKind);
        On<ExaminationEngagementOpened>(ApplyFact);
        On<ExaminationEngagementRevised>(ApplyFact);
        On<TypeIBaselineFrozen>(ApplyFact);
        On<TypeIBaselineAmended>(ApplyFact);
    }
    public Uuid TenantId => _tenant;
    public Uuid ProgramId => Header?.ProgramId ?? _program;
    public long Revision => Header?.Revision ?? 0;
    public ExaminationEngagementHeader? Header { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public ImmutableContentRef? InitialContent { get; private set; }
    public ImmutableContentRef? BaselineContent { get; private set; }
    public ImmutableContentRef? SelectedBaselineContent => _selectedBaseline == Header?.BaselineId
        ? BaselineContent : _selectedBaselineContent;
    public Uuid SelectedBaselineId => _selectedBaseline;
    public DomainEvent? Latest { get; private set; }
    internal ExaminationEngagementReplayCut ReplayCut { get; } = new();

    public Result PrepareCreate(Uuid program) => Header is not null
        ? Fail(RequestErrorKind.Conflict, "The examination engagement already exists.")
        : _tenant == Uuid.Empty || program == Uuid.Empty || Id == Uuid.Empty
            ? Fail(RequestErrorKind.Validation, "An examination requires exact tenant/program/engagement identities.")
            : Result.Success;
    public Result PrepareWrite(Uuid program, long expected, bool requireFrozen)
    {
        if (Header is null || Header.ProgramId != program)
            return Fail(RequestErrorKind.NotFound, "The examination engagement was not found.");
        if (expected != Revision)
            return Fail(RequestErrorKind.Conflict, "The examination engagement revision is stale.");
        if ((Header.BaselineId is not null) != requireFrozen)
            return Fail(RequestErrorKind.Conflict, requireFrozen ? "A frozen Type I baseline is required."
                : "A frozen engagement can change only through an attributable baseline amendment.");
        return Result.Success;
    }

    internal Result Accept(OperationProvenance operation, ExaminationEngagementState state,
        ImmutableContentRef change, ImmutableContentRef response, ImmutableContentRef result)
    {
        var view = state.Engagement;
        var creating = operation.CommandKind == nameof(CreateExaminationEngagement);
        var frozen = operation.CommandKind == nameof(AmendTypeIBaseline);
        var baseline = operation.CommandKind is nameof(FreezeTypeIBaseline) or nameof(AmendTypeIBaseline);
        if (operation.CommandKind is not (nameof(CreateExaminationEngagement) or nameof(ReviseExaminationEngagement) or
                nameof(FreezeTypeIBaseline) or nameof(AmendTypeIBaseline)) ||
            !creating && operation.OriginalExpectedRevision is null ||
            view.Status != (baseline ? "baseline_frozen" : "draft") || baseline != (state.Baseline is not null) ||
            view.ActorDisplay != operation.Actor.Display || change.Schema != ExaminationEngagementState.Schema ||
            response.Schema != FirstResponses.Schema || result.Schema != "mutation-result-content.v1")
            return Fail(RequestErrorKind.Conflict, "The prepared engagement has another lifecycle or result/content schema.");
        if (state.Baseline is { } selected && (selected.TenantId != _tenant || selected.ProgramId != view.ProgramId ||
            selected.ExaminationEngagementId != Id || selected.TypeIBaselineId != operation.LogicalId ||
            selected.Revision != (Header?.BaselineRevision ?? 0) + 1 || selected.ActorMemberId != Member(operation) ||
            selected.ActorDisplay != operation.Actor.Display || selected.UpdatedAt != operation.AcceptedAt ||
            selected.Status != (frozen ? "amended" : "frozen")))
            return Fail(RequestErrorKind.Conflict, "The prepared baseline differs from its exact root/actor/revision authority.");
        var guard = creating ? PrepareCreate(view.ProgramId) : PrepareWrite(view.ProgramId,
            checked((long)operation.OriginalExpectedRevision!.Value), frozen);
        if (!guard.IsSuccess)
            return guard;
        // The fold would reject these after RaiseEvent; refuse the decision first so no fact is raised.
        if (creating && (view.ProgramId != _program || view.EngagementType != "type_i" || view.BoundaryId == Uuid.Empty))
            return Fail(RequestErrorKind.Validation,
                "An engagement must be created under its own program, as Type I, with an approved boundary.");
        if (!OwnRoot(operation, operation.CommandKind, creating ? null : (ulong)Revision) ||
            view.TenantId != _tenant || view.ExaminationEngagementId != Id || view.Revision != Revision + 1 ||
            view.ActorMemberId != Member(operation) || view.UpdatedAt != operation.AcceptedAt ||
            Header is { } header && (view.EngagementType != header.EngagementType || view.BoundaryId != header.BoundaryId))
            return Fail(RequestErrorKind.Conflict, "The prepared engagement differs from its exact root/actor/revision authority.");
        if (ReplayCut.Require(this, Acceptance, operation) is { IsSuccess: false } missing)
            return missing;
        DomainEvent fact = operation.CommandKind switch
        {
            nameof(CreateExaminationEngagement) => new ExaminationEngagementOpened(_tenant, Id, view.ProgramId,
                view.Revision, view.EngagementType, view.BoundaryId, null, 0, change, operation.AcceptedAt,
                operation, response, result, view.ActorMemberId),
            nameof(ReviseExaminationEngagement) => new ExaminationEngagementRevised(_tenant, Id, view.ProgramId,
                view.Revision, view.EngagementType, view.BoundaryId, null, 0, change, operation.AcceptedAt,
                operation, response, result, view.ActorMemberId),
            nameof(FreezeTypeIBaseline) => new TypeIBaselineFrozen(_tenant, Id, view.ProgramId, view.Revision,
                view.EngagementType, view.BoundaryId, state.Baseline!.TypeIBaselineId, state.Baseline.Revision,
                change, operation.AcceptedAt, operation, response, result, view.ActorMemberId),
            nameof(AmendTypeIBaseline) => new TypeIBaselineAmended(_tenant, Id, view.ProgramId, view.Revision,
                view.EngagementType, view.BoundaryId, state.Baseline!.TypeIBaselineId, state.Baseline.Revision,
                change, operation.AcceptedAt, operation, response, result, view.ActorMemberId),
            _ => throw new InvalidOperationException("The examination root kind is unsupported.")
        };
        RaiseEvent(fact);
        return Result.Success;
    }

    internal bool OwnRoot(OperationProvenance operation, string kind, ulong? expected) =>
        NativeOwnerHistory.IsOwnRoot(this, operation, kind, expected) && Member(operation) != Uuid.Empty;
    internal static Uuid Member(OperationProvenance operation) => operation.Actor.Kind == "member" &&
        Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) ? member : Uuid.Empty;
    internal static string Kind(DomainEvent fact) => fact switch
    {
        ExaminationEngagementOpened => nameof(CreateExaminationEngagement),
        ExaminationEngagementRevised => nameof(ReviseExaminationEngagement),
        TypeIBaselineFrozen => nameof(FreezeTypeIBaseline),
        TypeIBaselineAmended => nameof(AmendTypeIBaseline),
        _ => throw new ImmutableContentIntegrityException("The native engagement fact kind is unsupported.")
    };
    void ApplyFact(DomainEvent domainEvent)
    {
        var fact = (IExaminationEngagementFact)domainEvent;
        if (fact.TenantId != _tenant || fact.SubjectId != Id || fact.ProgramId == Uuid.Empty ||
            fact.Revision != Revision + 1 || fact.Change.Schema != ExaminationEngagementState.Schema ||
            fact.OccurredAt != fact.Operation.AcceptedAt || fact.ActorMemberId != Member(fact.Operation) ||
            fact.FirstResponse is null || fact.OperationResult is null ||
            !OwnRoot(fact.Operation, Kind(domainEvent), Revision == 0 ? null : (ulong)Revision))
            throw Invalid("The native engagement fact differs from its exact scope/root/actor/revision.");
        var baseline = domainEvent is TypeIBaselineFrozen or TypeIBaselineAmended;
        if (Header is null)
        {
            if (domainEvent is not ExaminationEngagementOpened || fact.Operation.LogicalId != Id || baseline ||
                fact.BaselineId is not null || fact.BaselineRevision != 0 || fact.BoundaryId == Uuid.Empty ||
                fact.EngagementType != "type_i")
                throw Invalid("An engagement must be created once with exact initial authority.");
            InitialContent = fact.Change;
        }
        else
        {
            if (domainEvent is ExaminationEngagementOpened || Header.ProgramId != fact.ProgramId ||
                Header.EngagementType != fact.EngagementType || Header.BoundaryId != fact.BoundaryId ||
                domainEvent is ExaminationEngagementRevised && (Header.BaselineId is not null || baseline ||
                    fact.BaselineId is not null || fact.BaselineRevision != 0) ||
                domainEvent is TypeIBaselineFrozen && (Header.BaselineId is not null || fact.BaselineRevision != 1) ||
                domainEvent is TypeIBaselineAmended && (Header.BaselineId is null ||
                    fact.BaselineRevision != Header.BaselineRevision + 1))
                throw Invalid("The native engagement lifecycle or baseline lineage is invalid.");
        }
        if (baseline && (fact.BaselineId is not { } id || id == Uuid.Empty || fact.Operation.LogicalId != id))
            throw Invalid("A baseline root must retain its exact accepted baseline identity.");
        Header = new ExaminationEngagementHeader(fact.ProgramId, fact.EngagementType, fact.BoundaryId,
            baseline ? "baseline_frozen" : "draft", fact.Revision, fact.BaselineId, fact.BaselineRevision);
        CurrentContent = fact.Change;
        if (baseline)
        {
            BaselineContent = fact.Change;
            if (fact.BaselineId == _selectedBaseline)
                _selectedBaselineContent = fact.Change;
        }
        Latest = domainEvent;
        Acceptance.Observe(domainEvent);
        ReplayCut.Observe(domainEvent);
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
    static Result Fail(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
