using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Policies.Rules;

public static class PolicyFactContent
{
    public const string ContentSchema = "policy-authored-content.v1";
    public const string ChangeSchema = "policy-mutation-change.v1";
    public const string AuthoritySchema = "policy-current-authority.v1";

    public static async ValueTask<PolicyMutationContent> ReadChangeAsync(IImmutableContentStore content,
        IPolicyFact fact, CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Change.Schema != ChangeSchema || fact.FirstResponse is null || fact.OperationResult is null)
            throw new ImmutableContentIntegrityException("The Policy mutation has another content or response schema.");
        var mutation = await content.ReadJsonAsync<PolicyMutationContent>(fact.TenantId.ToString(), fact.Change, ct).ConfigureAwait(false);
        if (mutation.Change is null || !InScope(mutation.Change, fact.Header))
            throw new ImmutableContentIntegrityException("The Policy mutation has another exact owner scope.");
        DomainEvent expected;
        try
        {
            expected = PolicyFactFactory.Create(fact.Operation.CommandKind, fact.Header, fact.NativeSequence,
                fact.Operation, fact.Change, fact.Authority, fact.FirstResponse, fact.OperationResult, mutation);
        }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The Policy mutation has no exact selected native receipt.");
        }
        if (fact is not DomainEvent original || original.GetType() != expected.GetType() ||
            ContentDigest.Sha256(original, original.GetType()) != ContentDigest.Sha256(expected, expected.GetType()))
            throw new ImmutableContentIntegrityException("The native Policy fact differs from its original immutable receipt.");
        var authored = mutation.Change switch
        {
            PolicyDraftCreatedContent created => created.Content,
            PolicyDraftRevisedContent revised => revised.Content,
            _ => null,
        };
        if (authored is not null && (PolicyRules.ValidateContent(authored) is not null || PolicyRules.Hash(authored) != fact.Header.Content.Sha256))
            throw new ImmutableContentIntegrityException("The Policy authored body differs from its native content reference.");
        return mutation;
    }

    public static ValueTask<PolicyContent> ReadDraftAsync(IImmutableContentStore content, IPolicyFact fact,
        CancellationToken ct = default) => ReadContentAsync(content, fact, ct);

    public static async ValueTask<PolicyContent> ReadContentAsync(IImmutableContentStore content, IPolicyFact fact,
        CancellationToken ct = default)
    {
        ValidateScope(fact);
        if (fact.Header.Content.Schema != ContentSchema)
            throw new ImmutableContentIntegrityException("The Policy content has another schema.");
        var body = await content.ReadJsonAsync<PolicyContent>(fact.TenantId.ToString(), fact.Header.Content, ct).ConfigureAwait(false);
        if (PolicyRules.ValidateContent(body) is not null)
            throw new ImmutableContentIntegrityException("The Policy body violates its authored content contract.");
        return body;
    }

    static void ValidateScope(IPolicyFact fact)
    {
        if (fact.Header is not { } header || fact.Operation is null || header.TenantId != fact.TenantId ||
            header.PolicyId == Uuid.Empty || header.ProgramId == Uuid.Empty || header.Content is null || fact.Change is null || fact.Authority is null ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), Policy.Area, header.PolicyId.ToString()))
            throw new ImmutableContentIntegrityException("The native Policy fact has another exact owner scope.");
    }

    internal static bool InScope(IPolicyMutationContent change, PolicyAuthorityHeader header) => change switch
    {
        PolicyDraftCreatedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyDraftRevisedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyReviewedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyApprovedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyPeriodicReviewConfirmedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyRetirementProposedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyRetiredContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        PolicyDraftDiscardedContent item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.PolicyId == header.PolicyId,
        _ => false,
    };
}
