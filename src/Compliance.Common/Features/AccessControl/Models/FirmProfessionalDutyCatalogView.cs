using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Professional-duty designations ordered by designation id; each carries its own owner revision.</summary>
public sealed record FirmProfessionalDutyCatalogView(IReadOnlyList<FirmProfessionalDutyDesignationView> Designations);
