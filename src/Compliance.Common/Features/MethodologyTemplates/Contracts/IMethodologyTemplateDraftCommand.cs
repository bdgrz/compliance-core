using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;

/// <summary>A personal platform-operator action on one separate methodology draft.</summary>
public interface IMethodologyTemplateDraftCommand : IPlatformOperatorRequest
{
    const string AuthorPermission = "methodology.author";
    const string ApprovePermission = "methodology.approve";
    Uuid CommandId { get; }
    Uuid TemplateId { get; }
    Uuid DraftId { get; }
    string Permission { get; }
    bool RequiresHttp { get; }
}
