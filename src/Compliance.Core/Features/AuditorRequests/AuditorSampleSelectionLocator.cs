using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Read-only discovery of one admitted local selection; never mutation or disclosure authority.</summary>
public sealed record AuditorSampleSelectionLocator(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId,
    Uuid SampleSelectionId, DateTimeOffset AdmittedAt);
