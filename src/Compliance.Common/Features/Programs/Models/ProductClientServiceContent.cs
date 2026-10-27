namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProductClientServiceContent(string Name, string Purpose, string OwnerReference)
{
    public const string Schema = "product-client-service-content.v1";
}
