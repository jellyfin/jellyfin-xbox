using Jellyfin.Utils;
using Windows.UI.Xaml;

namespace Jellyfin.Controls;

/// <summary>
/// A state trigger that activates when the current device family matches the specified <see cref="TargetDeviceFamily"/>.
/// </summary>
public class DeviceFamilyStateTrigger : StateTriggerBase
{
    /// <summary>
    /// Identifies the <see cref="TargetDeviceFamily"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetDeviceFamilyProperty = DependencyProperty.Register(
        "TargetDeviceFamily", typeof(DeviceFormFactorType), typeof(DeviceFamilyStateTrigger), new PropertyMetadata(default(DeviceFormFactorType), OnDeviceTypePropertyChanged));

    /// <summary>
    /// Gets or sets the target device family that activates this trigger.
    /// </summary>
    public DeviceFormFactorType TargetDeviceFamily
    {
        get { return (DeviceFormFactorType)GetValue(TargetDeviceFamilyProperty); }
        set { SetValue(TargetDeviceFamilyProperty, value); }
    }

    /// <summary>
    /// Called when the <see cref="TargetDeviceFamily"/> property changes.
    /// </summary>
    /// <param name="dependencyObject">The object on which the property changed.</param>
    /// <param name="eventArgs">Details about the property change.</param>
    private static void OnDeviceTypePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var trigger = (DeviceFamilyStateTrigger)dependencyObject;
        var newTargetDeviceFamily = (DeviceFormFactorType)eventArgs.NewValue;
        trigger.SetActive(newTargetDeviceFamily == AppUtils.GetDeviceFormFactorType());
    }
}
