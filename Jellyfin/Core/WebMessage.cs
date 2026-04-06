using Windows.Data.Json;

namespace Jellyfin.Core;

/// <summary>
/// Model for a message received from the webUI.
/// </summary>
/// <param name="Type">The defined message type.</param>
/// <param name="Args">Message contents.</param>
public record WebMessage(string Type, JsonObject Args);
