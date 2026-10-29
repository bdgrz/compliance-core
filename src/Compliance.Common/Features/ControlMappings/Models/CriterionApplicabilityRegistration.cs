using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record CriterionApplicabilityRegistration(Uuid DecisionId, long Revision,
    int VersionNumber);
