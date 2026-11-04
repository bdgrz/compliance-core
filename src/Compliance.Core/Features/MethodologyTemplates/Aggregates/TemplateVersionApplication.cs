using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Records one terminal owning-context outcome for every entry of one applied template version.</summary>
public sealed class TemplateVersionApplication : Aggregate
{
    public const string Area = "template-version-applications";

    public TemplateVersionApplication(Uuid tenantId, Uuid programId, Uuid templateId, long version)
        : base(Identity(tenantId, programId, templateId, version),
            new EventStreamAddress(tenantId.ToString(), Area, Identity(tenantId, programId, templateId, version).ToString()))
    {
    }

    public bool IsOpened => false;
    public bool IsCompleted => false;
    public TemplateApplicationOutcomeCounts Counts => new(0, 0, 0);

    public static Uuid Identity(Uuid tenantId, Uuid programId, Uuid templateId, long version) =>
        Uuid.CreateVersion5(tenantId, $"template-version-application:{programId}:{templateId}:{version}");

    public Result Open(Uuid applicationEventId, IReadOnlyList<string> entryKeys) =>
        Result.Failure(new RequestError(RequestErrorKind.NotFound, "The template version application is not open.", true));

    public Result Record(TemplateEntryOutcome outcome) =>
        Result.Failure(new RequestError(RequestErrorKind.NotFound, "The template version application is not open.", true));
}
