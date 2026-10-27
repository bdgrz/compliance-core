using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ApproveMethodologyTemplateVersionHandler(MethodologyTemplateOperations operations) : IRequestHandler<ApproveMethodologyTemplateVersion>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApproveMethodologyTemplateVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        return operations.ExecuteAsync(context, request.TemplateId, request.CommandId, nameof(ApproveMethodologyTemplateVersion),
            null, (owner, member) => owner.PreparePublish(request.Version, member),
            (rules, member, display, at) => rules.ApproveVersion(rules.Revision, request.ApprovalRationale, member, display, at), ct);
    }
}
