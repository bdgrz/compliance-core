using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
namespace Bdgrz.Compliance.Features.Policies.Rules;

public static class PolicyResponses
{
    public const string Registration = "policy-registration.v1";
    public const string Decision = "policy-decision.v1";
    public const string Version = "policy-version.v1";
    public static string Schema(string kind) => kind switch
    {
        nameof(CreatePolicyDraft) or nameof(RevisePolicyDraft) or nameof(ProposePolicySuccessor) or nameof(ProposePolicyRetirement) => Registration,
        nameof(ReviewPolicyDraft) or nameof(ConfirmPolicyReview) or nameof(ApprovePolicyRetirement) => Decision,
        nameof(ApprovePolicy) => Version,
        nameof(DiscardPolicyDraft) => UnitResult.Schema,
        _ => throw new InvalidOperationException("The Policy result schema is not selected."),
    };
    public static ValueTask<FirstResponseDescriptor> FreezeAsync(IImmutableContentStore content, PolicyRules rules,
        string kind, Uuid command, CancellationToken ct = default) => kind switch
        {
            nameof(CreatePolicyDraft) or nameof(RevisePolicyDraft) or nameof(ProposePolicySuccessor) or nameof(ProposePolicyRetirement) =>
                FreezeAsync(content, rules.TenantId.ToString(), Registration,
                    new PolicyRegistration(rules.Id, rules.Identifier!, rules.Revision), ct),
            nameof(ReviewPolicyDraft) or nameof(ConfirmPolicyReview) or nameof(ApprovePolicyRetirement) =>
                FreezeAsync(content, rules.TenantId.ToString(), Decision, rules.FindDecision(command)!, ct),
            nameof(ApprovePolicy) => FreezeAsync(content, rules.TenantId.ToString(), Version, rules.CurrentVersion!, ct),
            nameof(DiscardPolicyDraft) => FreezeAsync(content, rules.TenantId.ToString(), UnitResult.Schema, new UnitResult(), ct),
            _ => throw new InvalidOperationException("The Policy result schema is not selected."),
        };
    static async ValueTask<FirstResponseDescriptor> FreezeAsync<T>(IImmutableContentStore content, string realm,
        string schema, T value, CancellationToken ct) where T : notnull
    {
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), schema, value, ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));
    }
}
