using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Revises the current methodology-template draft with an explicit content revision.</summary>
[Discriminator("bdgrz.methodology-templates.revise-methodology-template-draft", 1)]
public sealed record ReviseMethodologyTemplateDraft(Uuid TemplateId, long ExpectedRevision,
    MethodologyTemplateContent Content, string ChangeNotes) : IRequest, IPlatformOperatorRequest, ICallable;
