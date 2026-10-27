using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Aggregates;

/// <summary>A bounded native line owner; full history is composed explicitly by its verified reader.</summary>
public sealed class ControlCriterionMapping : Aggregate
{
    public const string Area = "control-criterion-mappings";
    public const int MaximumTextLength = 4000;
    public const string RegistrationSchema = "ControlCriterionMapping.Registration.v1";
    public ControlCriterionMapping(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId, string criterionIdentifier, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, programId, controlId, editionId, criterionIdentifier), new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, programId, controlId, editionId, criterionIdentifier).ToString()))
    {
        ProgramId = programId;
        EditionId = editionId;
        CriterionIdentifier = criterionIdentifier;
        ControlId = controlId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ControlCriterionMappingProposed>(ev => ApplyFact(ev));
        On<ControlCriterionMappingReviewed>(ev => ApplyFact(ev));
        On<ControlCriterionMappingRetired>(ev => ApplyFact(ev));
    }
    public NativeAcceptance Acceptance { get; }
    public MappingLineHeader Header { get; private set; } = new(0, 0, Uuid.Empty, null, null, false);
    public ImmutableContentRef? CurrentContent { get; private set; }
    public Uuid ProgramId { get; }
    public Uuid EditionId { get; }
    public string CriterionIdentifier { get; }
    public Uuid ControlId { get; }
    public long Revision => Header.Revision;
    public static Uuid IdentityOf(Uuid tenantId, Uuid programId, Uuid controlId, Uuid editionId,
        string criterionIdentifier) => AggregateIdentity.Create("ControlCriterionMapping", tenantId, programId,
        controlId, editionId, criterionIdentifier);

    public Result Apply(MappingLinePrepared prepared, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared.BeforeRevision != Revision)
            return Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "control criterion mapping", Revision)).ToRequestError());
        if (prepared.OwnerVersion != Version ||
            prepared.PhysicalPosition != CommittedStreamPosition || prepared.ReplayDigest != ReplayDigest ||
            prepared.Fact is not IMappingLineFact)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The line changed while immutable content was prepared.", true));
        switch (prepared.Fact)
        {
            case ControlCriterionMappingProposed ev:
                RaiseEvent(ev with { FirstResponse = firstResponse });
                break;
            case ControlCriterionMappingReviewed ev:
                RaiseEvent(ev with { FirstResponse = firstResponse });
                break;
            case ControlCriterionMappingRetired ev:
                RaiseEvent(ev with { FirstResponse = firstResponse });
                break;
            default:
                throw new InvalidOperationException("The prepared fact belongs to another line family.");
        }
        return Result.Success;
    }

    internal string ReplayDigest { get; private set; } = MappingLineReplayDigest.Empty;

    protected override void Apply(DomainEvent ev)
    {
        ReplayDigest = MappingLineReplayDigest.Append(ReplayDigest, ev);
        base.Apply(ev);
    }

    void ApplyFact(DomainEvent fact)
    {
        var next = MappingLineTransitions.Next(this, Header, fact);
        if (Acceptance.SelectedFact is not null &&
            Acceptance.SelectedLogicalId == ((IMappingLineFact)fact).Operation.LogicalId)
            throw new ImmutableContentIntegrityException("The selected native operation occurs more than once on its line.");
        Acceptance.Observe(fact);
        Header = next;
        CurrentContent = ((IMappingLineFact)fact).Content;
    }
}
