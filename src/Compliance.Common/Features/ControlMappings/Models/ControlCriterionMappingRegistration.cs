using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record ControlCriterionMappingRegistration(Uuid MappingId, long Revision,
    int VersionNumber);
