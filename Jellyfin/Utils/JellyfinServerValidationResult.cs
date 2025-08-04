using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Core;

namespace Jellyfin.Utils;

/// <summary>
/// Defines the result of a Jellyfin server validation check.
/// </summary>
public record JellyfinServerValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinServerValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">True if the validation succeded, otherwise false.</param>
    /// <param name="errorMessage">The error message.</param>
    public JellyfinServerValidationResult(bool isValid, string errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the Jellyfin server entered is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if the validation failed.
    /// </summary>
    public string ErrorMessage { get; set; }
}
