using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>A bounded current provider dependency on an application's reviewed system.</summary>
public sealed record ApplicationVendorReferenceView(Uuid ProviderId, string ProviderName,
    string ProviderKind, long ProviderRevision, string? Materiality, bool Subservice,
    string? BoundaryTreatment, int DependencyIndex, Uuid SystemInstanceId,
    long? SystemInstanceRevision, long? ApplicationRevision, string Rationale,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntilExclusive);
