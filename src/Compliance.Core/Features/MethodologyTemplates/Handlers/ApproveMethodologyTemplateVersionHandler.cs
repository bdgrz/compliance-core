using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

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
