using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public static class BoundaryFactContent
{
    public const string ContentSchema = "boundary-authored-content.v1";
    public const string ChangeSchema = "boundary-mutation-change.v1";
    public const string AuthoritySchema = "boundary-current-authority.v1";
    public static async ValueTask<BoundaryMutationContent> ReadChangeAsync(IImmutableContentStore content,
        IBoundaryFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Change.Schema != ChangeSchema || fact.FirstResponse is null || fact.OperationResult is null)
            throw new ImmutableContentIntegrityException("The boundary change has another content or response schema.");
        var mutation = await content.ReadJsonAsync<BoundaryMutationContent>(fact.TenantId.ToString(), fact.Change, ct).ConfigureAwait(false);
        if (fact.ChangeCount is < 0 or > 1 || mutation.Changes is null || mutation.Changes.Count != fact.ChangeCount ||
            mutation.Changes.Any(change => !InScope(change, fact.Header)) ||
            mutation.Receipt is { } receipt && !InScope(receipt, fact.Header) ||
            (mutation.Changes.Count == 0) != (mutation.Receipt is not null))
            throw new ImmutableContentIntegrityException("The boundary change differs from its exact native scope and receipt.");
        DomainEvent expected;
        try
        {
            expected = BoundaryFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
                fact.Operation, fact.Change, fact.Authority, fact.FirstResponse, fact.OperationResult, mutation);
        }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The boundary mutation has no exact selected native receipt.");
        }
        if (fact is not DomainEvent original || original.GetType() != expected.GetType() ||
            ContentDigest.Sha256(original, original.GetType()) != ContentDigest.Sha256(expected, expected.GetType()))
            throw new ImmutableContentIntegrityException("The native boundary receipt differs from its immutable original mutation.");
        var authored = mutation.Changes.Select(change => change switch
        {
            BoundaryDraftCreatedContent created => created.Content,
            BoundaryDraftRevisedContent revised => revised.Content,
            BoundarySuccessorProposedContent proposed => proposed.Content,
            _ => null,
        }).OfType<BoundaryContent>().SingleOrDefault();
        if (authored is not null && ContentDigest.OfJson(authored) != fact.Header.Content.Sha256)
            throw new ImmutableContentIntegrityException("The boundary authored mutation differs from its native content reference.");
        return mutation;
    }
    public static ValueTask<BoundaryContent> ReadDraftAsync(IImmutableContentStore content, IBoundaryFact fact,
        CancellationToken ct = default) => ReadContentAsync(content, fact, ct);
    public static async ValueTask<BoundaryContent> ReadContentAsync(IImmutableContentStore content,
        IBoundaryFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Header.Content.Schema != ContentSchema)
            throw new ImmutableContentIntegrityException("The boundary authored body has another schema.");
        var body = await content.ReadJsonAsync<BoundaryContent>(fact.TenantId.ToString(), fact.Header.Content, ct).ConfigureAwait(false);
        if (BoundaryRules.Validate(body) is not null)
            throw new ImmutableContentIntegrityException("The boundary authored body violates its exact current content contract.");
        return body;
    }
    internal static void ValidateScope(IBoundaryFact fact)
    {
        if (fact.Header is not { } header || fact.Operation is null || header.TenantId != fact.TenantId || header.BoundaryId == Uuid.Empty ||
            header.ProgramId == Uuid.Empty || header.Content is null || fact.Change is null || fact.Authority is null ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), SystemBoundary.Area, header.BoundaryId.ToString()))
            throw new ImmutableContentIntegrityException("The native boundary fact has another exact owner scope.");
        var valid = fact switch
        {
            BoundaryDraftCreated data => data.ProgramId == header.ProgramId && data.BoundaryId == header.BoundaryId,
            BoundaryDraftRevised data => data.BoundaryId == header.BoundaryId,
            BoundarySuccessorProposed data => data.BoundaryId == header.BoundaryId,
            BoundaryReviewed data => data.BoundaryId == header.BoundaryId,
            BoundaryApproved data => data.BoundaryId == header.BoundaryId,
            BoundaryDraftDiscarded data => data.BoundaryId == header.BoundaryId,
            ResponsibilityAssigned data => data.Scope is { } scope && scope.RecordType == "boundary" && scope.RecordId == header.BoundaryId,
            ResponsibilityRevoked data => data.Scope is { } scope && scope.RecordType == "boundary" && scope.RecordId == header.BoundaryId,
            _ => false,
        };
        if (!valid)
            throw new ImmutableContentIntegrityException("The boundary receipt metadata has another exact owner scope.");
    }
    internal static bool InScope(IBoundaryMutationContent change, BoundaryAuthorityHeader header) => change switch
    {
        BoundaryDraftCreatedContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.BoundaryId == header.BoundaryId,
        BoundaryDraftRevisedContent data => data.TenantId == header.TenantId && data.BoundaryId == header.BoundaryId,
        BoundarySuccessorProposedContent data => data.TenantId == header.TenantId && data.BoundaryId == header.BoundaryId,
        BoundaryReviewedContent data => data.TenantId == header.TenantId && data.BoundaryId == header.BoundaryId,
        BoundaryApprovedContent data => data.TenantId == header.TenantId && data.BoundaryId == header.BoundaryId,
        BoundaryDraftDiscardedContent data => data.TenantId == header.TenantId && data.BoundaryId == header.BoundaryId,
        ResponsibilityAssigned data => data.TenantId == header.TenantId && data.Scope is { } scope && scope.RecordType == "boundary" && scope.RecordId == header.BoundaryId,
        ResponsibilityRevoked data => data.TenantId == header.TenantId && data.Scope is { } scope && scope.RecordType == "boundary" && scope.RecordId == header.BoundaryId,
        _ => false,
    };
}
