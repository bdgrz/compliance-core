using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Commitments.Services;

public static class CommitmentFactContent
{
    public const string ContentSchema = "commitment-authored-content.v1";
    public const string ChangeSchema = "commitment-mutation-change.v1";
    public const string AuthoritySchema = "commitment-current-authority.v1";
    public static async ValueTask<CommitmentMutationContent> ReadChangeAsync(IImmutableContentStore content,
        ICommitmentFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Change.Schema != ChangeSchema || fact.FirstResponse is null || fact.OperationResult is null)
            throw new ImmutableContentIntegrityException("The commitment change has another content or response schema.");
        var mutation = await content.ReadJsonAsync<CommitmentMutationContent>(fact.TenantId.ToString(), fact.Change, ct).ConfigureAwait(false);
        if (fact.ChangeCount is < 0 or > 1 || mutation.Changes is null || mutation.Changes.Count != fact.ChangeCount || mutation.Changes.Any(change => !InScope(change, fact.Header)) ||
            mutation.Receipt is { } receipt && !InScope(receipt, fact.Header) ||
            mutation.Changes.Count == 0 != (mutation.Receipt is not null))
            throw new ImmutableContentIntegrityException("The commitment change differs from its exact native scope and receipt.");
        DomainEvent expected;
        try
        {
            expected = CommitmentFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
                fact.Operation, fact.Change, fact.Authority, fact.FirstResponse, fact.OperationResult, mutation);
        }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The commitment mutation has no exact selected native receipt.");
        }
        if (fact is not DomainEvent original || original.GetType() != expected.GetType() ||
            ContentDigest.Sha256(original, original.GetType()) != ContentDigest.Sha256(expected, expected.GetType()))
            throw new ImmutableContentIntegrityException("The native commitment receipt differs from its immutable original mutation.");
        var authored = mutation.Changes.Select(change => change switch
        {
            CommitmentDraftCreatedContent created => new CommitmentDraftContent(created.Statement, created.Context, created.SourceReference),
            CommitmentDraftRevisedContent revised => new CommitmentDraftContent(revised.Statement, revised.Context, revised.SourceReference),
            _ => null,
        }).OfType<CommitmentDraftContent>().SingleOrDefault();
        if (authored is not null && (ContentDigest.OfJson(authored) != fact.Header.Content.Sha256 || authored.SourceReference != fact.Header.SourceReference))
            throw new ImmutableContentIntegrityException("The commitment authored mutation differs from its native content reference.");
        return mutation;
    }
    public static ValueTask<CommitmentDraftContent> ReadDraftAsync(IImmutableContentStore content, ICommitmentFact fact,
        CancellationToken ct = default) => ReadContentAsync(content, fact, ct);
    public static async ValueTask<CommitmentDraftContent> ReadContentAsync(IImmutableContentStore content,
        ICommitmentFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Header.Content.Schema != ContentSchema)
            throw new ImmutableContentIntegrityException("The commitment authored body has another schema.");
        var body = await content.ReadJsonAsync<CommitmentDraftContent>(fact.TenantId.ToString(), fact.Header.Content, ct).ConfigureAwait(false);
        if (CommitmentRules.Validate(fact.Header.Kind, fact.Header.Identifier, body.Statement, body.Context,
            body.SourceReference, fact.Header.ProviderId) is not null || body.SourceReference != fact.Header.SourceReference)
            throw new ImmutableContentIntegrityException("The commitment authored body violates its exact current content contract.");
        return body;
    }
    internal static void ValidateScope(ICommitmentFact fact)
    {
        if (fact.Header is not { } header || fact.Operation is null || header.TenantId != fact.TenantId || header.DraftId == Uuid.Empty ||
            header.ProgramId == Uuid.Empty || header.Content is null || fact.Change is null || fact.Authority is null ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), CommitmentDraft.Area, header.DraftId.ToString()))
            throw new ImmutableContentIntegrityException("The native commitment fact has another exact owner scope.");
        var valid = fact switch
        {
            CommitmentDraftCreated data => data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
            CommitmentDraftRevised data => data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
            CommitmentReviewed data => data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
            CommitmentApproved data => data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
            CommitmentRetired data => data.ProgramId == header.ProgramId && data.SubjectId == header.DraftId,
            ResponsibilityAssigned data => data.Scope is { } scope && scope.RecordType == SeparationOfDutiesRecordTypes.Commitment && scope.RecordId == header.DraftId,
            ResponsibilityRevoked data => data.Scope is { } scope && scope.RecordType == SeparationOfDutiesRecordTypes.Commitment && scope.RecordId == header.DraftId,
            _ => false,
        };
        if (!valid)
            throw new ImmutableContentIntegrityException("The commitment receipt metadata has another exact owner scope.");
    }
    internal static bool InScope(ICommitmentMutationContent change, CommitmentAuthorityHeader header) => change switch
    {
        CommitmentDraftCreatedContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
        CommitmentDraftRevisedContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
        CommitmentReviewedContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
        CommitmentApprovedContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.DraftId == header.DraftId,
        CommitmentRetiredContent data => data.TenantId == header.TenantId && data.ProgramId == header.ProgramId && data.SubjectId == header.DraftId,
        ResponsibilityAssigned data => data.TenantId == header.TenantId && data.Scope is { } scope && scope.RecordType == SeparationOfDutiesRecordTypes.Commitment && scope.RecordId == header.DraftId,
        ResponsibilityRevoked data => data.TenantId == header.TenantId && data.Scope is { } scope && scope.RecordType == SeparationOfDutiesRecordTypes.Commitment && scope.RecordId == header.DraftId,
        _ => false,
    };
}
