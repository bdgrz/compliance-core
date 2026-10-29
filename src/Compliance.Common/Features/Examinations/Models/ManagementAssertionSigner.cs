using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An identified management signer and their accountable office.</summary>
public sealed record ManagementAssertionSigner(Uuid MemberId, string Role);
