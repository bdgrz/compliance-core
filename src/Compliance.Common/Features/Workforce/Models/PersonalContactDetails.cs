namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>Restricted personal contact details for a workforce person.</summary>
public sealed record PersonalContactDetails(string? PersonalEmail = null, string? PersonalPhone = null);
