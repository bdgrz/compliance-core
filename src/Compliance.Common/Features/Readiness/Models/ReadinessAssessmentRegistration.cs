using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Readiness.Models;

public sealed record ReadinessAssessmentRegistration(Uuid AssessmentId, long Revision);
