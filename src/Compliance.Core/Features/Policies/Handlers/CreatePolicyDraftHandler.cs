using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class CreatePolicyDraftHandler(PolicyOperations operations) : IRequestHandler<CreatePolicyDraft, PolicyRegistration>
{
    public ValueTask<Result<PolicyRegistration>> HandleAsync(IRequestContext<CreatePolicyDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PolicyId != Policy.IdFor(request.TenantId, request.ProgramId, request.Identifier))
            return ValueTask.FromResult(Result<PolicyRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The authored PolicyId must equal the deterministic normalized identifier identity.")));
        return operations.ExecuteAsync<CreatePolicyDraft, PolicyRegistration>(context, request.TenantId, request.ProgramId,
            request.PolicyId, request.PolicyId, nameof(CreatePolicyDraft), null,
            (_, _, _, _) => PolicyOperations.Ready(),
            (rules, actor, at) =>
            {
                var result = rules.Create(request.ProgramId, request.PolicyId, request.Identifier, request.Content, actor.Reference, actor.MemberId, at);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
            }, null, ct);
    }
}
