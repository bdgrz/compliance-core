using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Aggregates;

/// <summary>Owns one directed application relationship and its local lifecycle revision.</summary>
public sealed class ApplicationRelationship : Aggregate
{
    public const string Area = "application-relationships";

    readonly Uuid _tenantId;
    readonly Uuid _relationshipId;
    ApplicationRelationshipAdded? _added;
    long _revision;
    string? _status;

    public ApplicationRelationship(Uuid tenantId, Uuid relationshipId,
        Uuid selectedLogicalId = default)
        : base(relationshipId, new EventStreamAddress(tenantId.ToString(), Area,
            relationshipId.ToString()))
    {
        _tenantId = tenantId;
        _relationshipId = relationshipId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ApplicationRelationshipAdded>(Apply);
    }

    public NativeAcceptance Acceptance { get; }
    public long Revision => _revision;
    public string? Status => _status;
    public Uuid? SourceApplicationId => _added?.ApplicationId;
    public Uuid? TargetApplicationId => _added?.TargetApplicationId;
    public string? RelationshipType => _added?.RelationshipType;

    public Result PrepareAdd(long expectedRevision, Uuid sourceApplicationId,
        Uuid targetApplicationId, string relationshipType, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (expectedRevision != 0)
            return Failure(RequestErrorKind.Conflict,
                "A new application relationship expects revision 0.");
        if (_revision != 0)
            return Failure(RequestErrorKind.Conflict,
                "The application relationship already exists.");
        if (_tenantId == Uuid.Empty || sourceApplicationId == Uuid.Empty ||
            targetApplicationId == Uuid.Empty || sourceApplicationId == targetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            _relationshipId != ApplicationRelationshipIdentity.Id(
                sourceApplicationId, relationshipType, targetApplicationId) ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            changedAt == default)
            return Failure(RequestErrorKind.Validation,
                "The application relationship identity or author is invalid.");
        return Result.Success;
    }

    public Result Add(OperationProvenance operation, long expectedRevision,
        Uuid sourceApplicationId, Uuid targetApplicationId, string relationshipType,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.Owner != Stream || operation.LogicalId != _relationshipId ||
            operation.CommandKind != nameof(AddApplicationRelationship) ||
            operation.OriginalExpectedRevision != (ulong)expectedRevision)
            return Failure(RequestErrorKind.Validation,
                "The relationship operation must be the exact root creation on its own stream.");
        var prepared = PrepareAdd(expectedRevision, sourceApplicationId, targetApplicationId,
            relationshipType, actorMemberId, actorDisplay, changedAt);
        if (!prepared.IsSuccess)
            return prepared;
        var ev = new ApplicationRelationshipAdded(_tenantId, sourceApplicationId,
            _relationshipId, 1, targetApplicationId, relationshipType, actorMemberId,
            actorDisplay, changedAt)
        {
            Operation = operation,
            FirstResponse = firstResponse,
            OperationResult = operationResult,
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        RaiseEvent(ev);
        return Result.Success;
    }

    void Apply(ApplicationRelationshipAdded ev)
    {
        Acceptance.Observe(ev);
        if (_added is not null || ev.TenantId != _tenantId ||
            ev.RelationshipId != _relationshipId ||
            ev.RelationshipId != ApplicationRelationshipIdentity.Id(ev.ApplicationId,
                ev.RelationshipType, ev.TargetApplicationId) ||
            ev.ApplicationId == Uuid.Empty || ev.TargetApplicationId == Uuid.Empty ||
            ev.ApplicationId == ev.TargetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(ev.RelationshipType) ||
            ev.RelationshipRevision != 1 || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != _relationshipId ||
            ev.Operation.CommandKind != nameof(AddApplicationRelationship) ||
            ev.FirstResponse is null || ev.OperationResult is null ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException(
                "The relationship addition does not match its canonical owner and root operation.");
        _added = ev;
        _revision = 1;
        _status = ev.RelationshipType == ApplicationRelationshipIdentity.Replaces
            ? "proposed"
            : "active";
    }

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
