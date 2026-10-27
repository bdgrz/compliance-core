using Bdgrz.Compliance.Features.MethodologyTemplates;
using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Projections;

static class MethodologyTemplateDirectorySchema
{
    public static readonly KvDirectoryIndex<MethodologyTemplateCatalogIdentity> ByFirstPublished = new(
        "by_first_published", 1, static value =>
        [value.FirstPublishedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture), value.TemplateId.ToString()]);
    public static readonly KvDirectory<MethodologyTemplateCatalogIdentity, Uuid> Templates = new("templates",
        ComplianceCoreJsonContext.Default.MethodologyTemplateCatalogIdentity, static value => value.TemplateId,
        static id => [id.ToString()], [ByFirstPublished]);
    public static readonly KvDirectory<MethodologyTemplatePublishedIdentity, Uuid> Versions = new("published_versions",
        ComplianceCoreJsonContext.Default.MethodologyTemplatePublishedIdentity, static value => value.VersionId,
        static id => [id.ToString()], []);
}
