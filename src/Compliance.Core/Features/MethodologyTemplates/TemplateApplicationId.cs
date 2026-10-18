using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

static class TemplateApplicationId
{
    public static Uuid For(Uuid programId, Uuid templateId, string applicationKey) =>
        Uuid.CreateVersion5(programId,
            $"methodology-template-application:{templateId}:{applicationKey.Trim()}");
}
