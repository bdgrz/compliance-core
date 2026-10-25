namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Canonical private values digest preimage, separate from changing scalar attribution/correlation.</summary>
public sealed record PersonValues(string DisplayName, string? WorkEmail, PersonalContactDetails? PersonalContact);
