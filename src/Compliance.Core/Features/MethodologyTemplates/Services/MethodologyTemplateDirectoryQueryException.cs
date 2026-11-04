namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>The directory cursor does not belong to the requested projection scope and native ordering.</summary>
sealed class MethodologyTemplateDirectoryQueryException(string message) : Exception(message);
