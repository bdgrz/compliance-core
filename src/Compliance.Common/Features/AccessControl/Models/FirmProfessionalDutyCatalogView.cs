namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Professional-duty designations ordered by designation id; each carries its own owner revision.</summary>
public sealed record FirmProfessionalDutyCatalogView(IReadOnlyList<FirmProfessionalDutyDesignationView> Designations);
