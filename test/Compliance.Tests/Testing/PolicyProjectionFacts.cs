using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Freezes explicitly supplied projection payloads without admitting them on a business owner.</summary>
sealed class PolicyProjectionFacts
{
    static readonly ConditionalWeakTable<FitzApplicationPolicyReferenceDirectory, PolicyProjectionFacts> References = [];
    readonly Dictionary<(Uuid Tenant, Uuid Policy), PolicyRules> _rules = [];
    readonly Dictionary<(Uuid Tenant, Uuid Policy), long> _sequences = [];
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());

    public static FitzApplicationPolicyReferenceDirectory ReferenceDirectory(IKvClient client)
    {
        var facts = new PolicyProjectionFacts();
        var directory = new FitzApplicationPolicyReferenceDirectory(client, facts.Content);
        References.Add(directory, facts);
        return directory;
    }

    public static async ValueTask ApplyAsync(FitzApplicationPolicyReferenceDirectory directory,
        IPolicyMutationContent change, CancellationToken ct = default) =>
        await directory.ApplyAsync(await FreezeAsync(directory, change), ct);

    public static ValueTask ApplyAsync(FitzApplicationPolicyReferenceDirectory directory,
        DomainEvent native, CancellationToken ct = default) => directory.ApplyAsync(native, ct);

    public static Task<DomainEvent> FreezeAsync(FitzApplicationPolicyReferenceDirectory directory,
        IPolicyMutationContent change) => References.GetValue(directory, _ => throw new InvalidOperationException(
            "The projection scenario must use its own immutable content store.")).FreezeAsync(change);

    public Task<DomainEvent> FreezeAsync(IPolicyMutationContent change)
    {
        var (tenant, program, policy, logical, actor, at, kind) = change switch
        {
            PolicyDraftCreatedContent value => (value.TenantId, value.ProgramId, value.PolicyId, value.PolicyId,
                value.ActorMemberId, value.ChangedAt, nameof(CreatePolicyDraft)),
            PolicyDraftRevisedContent value => (value.TenantId, value.ProgramId, value.PolicyId, Uuid.CreateVersion4(),
                value.ActorMemberId, value.ChangedAt, value.PredecessorVersion is null ? nameof(RevisePolicyDraft) : nameof(ProposePolicySuccessor)),
            PolicyReviewedContent value => (value.TenantId, value.ProgramId, value.PolicyId, value.DecisionId,
                value.ActorMemberId, value.DecidedAt, nameof(ReviewPolicyDraft)),
            PolicyApprovedContent value => (value.TenantId, value.ProgramId, value.PolicyId, value.DecisionId,
                value.ActorMemberId, value.DecidedAt, nameof(ApprovePolicy)),
            PolicyPeriodicReviewConfirmedContent value => (value.TenantId, value.ProgramId, value.PolicyId, value.DecisionId,
                value.ActorMemberId, value.DecidedAt, nameof(ConfirmPolicyReview)),
            PolicyRetirementProposedContent value => (value.TenantId, value.ProgramId, value.PolicyId, Uuid.CreateVersion4(),
                value.ActorMemberId, value.ProposedAt, nameof(ProposePolicyRetirement)),
            PolicyRetiredContent value => (value.TenantId, value.ProgramId, value.PolicyId, value.DecisionId,
                value.ActorMemberId, value.DecidedAt, nameof(ApprovePolicyRetirement)),
            PolicyDraftDiscardedContent value => (value.TenantId, value.ProgramId, value.PolicyId, Uuid.CreateVersion4(),
                Uuid.Parse(value.Actor.Id, System.Globalization.CultureInfo.InvariantCulture), value.DiscardedAt, nameof(DiscardPolicyDraft)),
            _ => throw new InvalidOperationException("The fixture has another Policy change kind."),
        };
        var key = (tenant, policy);
        if (!_rules.TryGetValue(key, out var rules))
        {
            rules = new PolicyRules(tenant, policy);
            _rules.Add(key, rules);
            if (change is not PolicyDraftCreatedContent)
            {
                // An isolated malformed event needs an envelope, while keeping its exact supplied fields.
                var body = change is PolicyDraftRevisedContent revised ? revised.Content
                    : new PolicyContent("Policy", "Govern access", PolicyAudience.CoreSecurity, null, 12,
                        "Policy body", null, "Owner", []);
                rules.Apply(new PolicyDraftCreatedContent(tenant, program, policy, policy, "POL-01", body,
                    Policy.Hash(body), ActorReference.ForMember(actor, actor.ToString()), actor, at));
            }
        }
        rules.Apply(change);
        var owner = new Policy(tenant, policy);
        var operation = OwnerProvenance.For(owner, logical, kind, ActorReference.ForMember(actor, actor.ToString()),
            "projection-fixture", null, AuthoredIntentDigest.Compute(new PolicyMutationContent(change)), at);
        var sequence = _sequences.GetValueOrDefault(key) + 1;
        _sequences[key] = sequence;
        return PolicySeeds.FreezeAsync(Content, rules, change, operation, sequence);
    }
}
