using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProductClientServiceRetirement(string Rationale)
{
    public const string Schema = "product-client-service-retirement.v1";
}
