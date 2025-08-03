using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.System;
using Windows.UI.Xaml;

namespace Jellyfin.Utils;

/// <summary>
/// Represents the state of a gamepad, including tracking the previous state of the B button
/// and a timer for button cooldowns.
/// </summary>
public sealed class GamepadState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GamepadState"/> class.
    /// </summary>
    public GamepadState()
    {
        WasBPressed = false;
        ButtonCooldownTimer = new Stopwatch();
        ButtonCooldownTimer.Start();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the B button was previously pressed.
    /// </summary>
    public bool WasBPressed { get; set; }

    /// <summary>
    /// Gets or sets the stopwatch that tracks the time since the last B button press.
    /// </summary>
    public Stopwatch ButtonCooldownTimer { get; set; }
}
