namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>Bounded field names; the restricted changed values stay in verified person content.</summary>
public sealed record PersonDetailChanges(bool DisplayName, bool WorkEmail, bool PersonalContact);
