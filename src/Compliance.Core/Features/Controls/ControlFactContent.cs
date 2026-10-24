using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Verified native Control payloads; projections use these bodies without deciding owner policy.</summary>
public static class ControlFactContent
{
    public const string ChangeSchema = "control-mutation-change.v1";
    public const string AuthoritySchema = "control-current-authority.v1";
    public const string DraftSchema = "control-draft-content.v1";

    public static async ValueTask<ControlDraftContent> ReadDraftAsync(IImmutableContentStore content,
        IControlFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Header.Content.Schema != DraftSchema)
            throw new ImmutableContentIntegrityException("The control content reference has another schema.");
        var value = await content.ReadJsonAsync<ControlDraftContent>(fact.TenantId.ToString(), fact.Header.Content, ct)
            .ConfigureAwait(false);
        if (ControlRules.ValidateContent(value) is not null)
            throw new ImmutableContentIntegrityException("The control content violates its authored content rules.");
        return value;
    }

    public static async ValueTask<ControlMutationContent> ReadChangeAsync(IImmutableContentStore content,
        IControlFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Change.Schema != ChangeSchema)
            throw new ImmutableContentIntegrityException("The control mutation has another content schema.");
        var value = await content.ReadJsonAsync<ControlMutationContent>(fact.TenantId.ToString(), fact.Change, ct)
            .ConfigureAwait(false);
        if (value.Changes is null || value.Changes.Count != fact.ChangeCount ||
            value.Changes.Any(change => !InScope(change, fact.Header)) ||
            value.Receipt is { } receipt && !InScope(receipt, fact.Header) ||
            fact.FirstResponse is null || fact.OperationResult is null)
            throw new ImmutableContentIntegrityException("The control immutable mutation differs from its exact native scope.");
        DomainEvent expected;
        try
        {
            expected = ControlFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
                fact.Operation, fact.Change, fact.Authority, fact.FirstResponse, fact.OperationResult, value);
        }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The control mutation has no exact selected native receipt.");
        }
        if (fact is not DomainEvent native || native.GetType() != expected.GetType() ||
            ContentDigest.Sha256(native, native.GetType()) != ContentDigest.Sha256(expected, expected.GetType()))
            throw new ImmutableContentIntegrityException("The native Control fact differs from its immutable original change.");
        return value;
    }

    internal static void ValidateScope(IControlFact fact)
    {
        if (fact.Header is not { } header || header.TenantId != fact.TenantId || header.ControlId == Uuid.Empty ||
            header.ProgramId == Uuid.Empty || fact.Operation is null ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), ControlDraft.Area, header.ControlId.ToString()))
            throw new ImmutableContentIntegrityException("The control fact has another exact owner scope.");
        var scoped = fact switch
        {
            ControlDraftCreated value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlDraftRevised value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlDraftDiscarded value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlReviewed value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlApproved value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlSuccessorProposed value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlRetirementProposed value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlRetired value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlProposalWithdrawn value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ControlOwnerPersonDesignated value => value.ControlId == header.ControlId && value.ProgramId == header.ProgramId,
            ResponsibilityAssigned value => value.Scope.RecordType == SeparationOfDutiesRecordTypes.Control && value.Scope.RecordId == header.ControlId,
            ResponsibilityRevoked value => value.Scope.RecordType == SeparationOfDutiesRecordTypes.Control && value.Scope.RecordId == header.ControlId,
            _ => false,
        };
        if (!scoped)
            throw new ImmutableContentIntegrityException("The control fact metadata differs from its current authority scope.");
    }

    internal static bool InScope(IControlMutationContent change, ControlAuthorityHeader header) => change switch
    {
        ControlDraftCreatedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlDraftRevisedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlDraftDiscardedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlReviewedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlApprovedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlSuccessorProposedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlRetirementProposedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlRetiredContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlProposalWithdrawnContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ControlOwnerPersonDesignatedContent value => value.TenantId == header.TenantId && value.ProgramId == header.ProgramId && value.ControlId == header.ControlId,
        ResponsibilityAssigned value => value.TenantId == header.TenantId && value.Scope.RecordType == SeparationOfDutiesRecordTypes.Control && value.Scope.RecordId == header.ControlId,
        ResponsibilityRevoked value => value.TenantId == header.TenantId && value.Scope.RecordType == SeparationOfDutiesRecordTypes.Control && value.Scope.RecordId == header.ControlId,
        _ => false,
    };
}
