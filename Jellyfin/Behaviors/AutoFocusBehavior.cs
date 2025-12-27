using System;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Behaviors;

/// <summary>
/// Defines a behavior that automatically focuses on the associated object when it is attached.
/// </summary>
public class AutoFocusBehavior : DependencyObject, IBehavior
{
    /// <summary>
    /// Identifies the <see cref="FocusOnEnable"/> dependency property, which determines whether the element should
    /// receive focus when enabled.
    /// </summary>
    /// <remarks>This property is used to enable or disable the behavior of automatically setting focus to the
    /// associated element when it becomes enabled.</remarks>
    public static readonly DependencyProperty FocusOnEnableProperty = DependencyProperty.Register(
        nameof(FocusOnEnable), typeof(bool), typeof(AutoFocusBehavior), new PropertyMetadata(default(bool)));

    /// <summary>
    /// Gets or sets a value indicating whether the control should automatically receive focus when it is enabled.
    /// </summary>
    public bool FocusOnEnable
    {
        get { return (bool)GetValue(FocusOnEnableProperty); }
        set { SetValue(FocusOnEnableProperty, value); }
    }

    /// <inheritdoc />
    public DependencyObject AssociatedObject { get; private set; }

    /// <inheritdoc />
    public void Attach(DependencyObject associatedObject)
    {
        if (associatedObject is Control { FocusState: FocusState.Unfocused } element)
        {
            if (element.IsLoaded && element.IsEnabled)
            {
                element.Focus(FocusState.Programmatic);
            }
            else
            {
                element.Loaded += OnElementOnLoaded;
            }

            element.IsEnabledChanged += ElementOnIsEnabledChanged;
        }
        else
        {
            throw new InvalidOperationException("AutoFocusBehavior can only be attached to FrameworkElement.");
        }

        AssociatedObject = associatedObject;
    }

    private void ElementOnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (FocusOnEnable && (AssociatedObject is Control { IsEnabled: true } control))
        {
            control.Focus(FocusState.Programmatic);
        }
    }

    /// <inheritdoc />
    public void Detach()
    {
        var control = AssociatedObject as Control;
        control.Loaded -= OnElementOnLoaded;
        control.IsEnabledChanged -= ElementOnIsEnabledChanged;
    }

    private void OnElementOnLoaded(object s, RoutedEventArgs e)
    {
        // Focus the element when it is loaded
        if (AssociatedObject is Control { FocusState: FocusState.Unfocused, IsEnabled: true } control)
        {
            control.Focus(FocusState.Programmatic);
        }
    }
}
