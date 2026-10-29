using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Commands;

/// <summary>Records the apply methodology template intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.methodology-templates.apply-methodology-template", 1)]
public sealed record ApplyMethodologyTemplate(Uuid TenantId, Uuid ProgramId, Uuid TemplateId, long TemplateVersion, string ApplicationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
