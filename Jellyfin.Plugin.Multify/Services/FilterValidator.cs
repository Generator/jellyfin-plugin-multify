using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Multify.Destinations;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Validates filter configurations on config load/save.
/// </summary>
public class FilterValidator
{
    private readonly ILogger<FilterValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterValidator"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{FilterValidator}"/> interface.</param>
    public FilterValidator(ILogger<FilterValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the filter configuration for a base option.
    /// </summary>
    /// <param name="option">The base option to validate.</param>
    /// <returns>A list of validation errors (empty if valid).</returns>
    public List<string> Validate(BaseOption option)
    {
        var errors = new List<string>();

        // Validate UserFilterMode
        if (!Enum.IsDefined(typeof(FilterMode), option.UserFilterMode))
        {
            var error = $"Invalid UserFilterMode: {option.UserFilterMode}";
            errors.Add(error);
            _logger.LogWarning("Validation error for {WebhookName}: {Error}", option.WebhookName, error);
        }

        // Validate LibraryFilterMode
        if (!Enum.IsDefined(typeof(FilterMode), option.LibraryFilterMode))
        {
            var error = $"Invalid LibraryFilterMode: {option.LibraryFilterMode}";
            errors.Add(error);
            _logger.LogWarning("Validation error for {WebhookName}: {Error}", option.WebhookName, error);
        }

        // Validate UserFilter array
        if (option.UserFilter != null)
        {
            for (int i = 0; i < option.UserFilter.Length; i++)
            {
                if (option.UserFilter[i] == Guid.Empty)
                {
                    var error = $"UserFilter contains empty GUID at index {i}";
                    errors.Add(error);
                    _logger.LogWarning("Validation error for {WebhookName}: {Error}", option.WebhookName, error);
                }
            }
        }

        // Validate LibraryFilter array
        if (option.LibraryFilter != null)
        {
            for (int i = 0; i < option.LibraryFilter.Length; i++)
            {
                if (option.LibraryFilter[i] == Guid.Empty)
                {
                    var error = $"LibraryFilter contains empty GUID at index {i}";
                    errors.Add(error);
                    _logger.LogWarning("Validation error for {WebhookName}: {Error}", option.WebhookName, error);
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates filter configuration and logs a summary.
    /// </summary>
    /// <param name="option">The base option to validate.</param>
    /// <returns>True if valid; false otherwise.</returns>
    public bool ValidateAndLog(BaseOption option)
    {
        var errors = Validate(option);

        if (errors.Count == 0)
        {
            _logger.LogDebug("Filter configuration valid for {WebhookName}", option.WebhookName);
            return true;
        }

        _logger.LogWarning(
            "Filter configuration has {ErrorCount} error(s) for {WebhookName}: {Errors}",
            errors.Count,
            option.WebhookName,
            string.Join("; ", errors));

        return false;
    }
}
