using System;
using System.Collections.Generic;
using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Jellyfin.Behaviors;

internal class KeyEventTriggerBehavior : Trigger
{
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(
        nameof(Filter), typeof(List<KeyboardAccelerator>), typeof(KeyEventTriggerBehavior), new PropertyMetadata(default(List<KeyboardAccelerator>)));

    public List<KeyboardAccelerator> Filter
    {
        get
        {
            var keyboardAccelerators = (List<KeyboardAccelerator>)GetValue(FilterProperty);
            if (keyboardAccelerators == null)
            {
                keyboardAccelerators = new List<KeyboardAccelerator>();
                SetValue(FilterProperty, keyboardAccelerators);
            }

            return keyboardAccelerators;
        }

        set
        {
            SetValue(FilterProperty, value);
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is not TextBox element)
        {
            throw new InvalidOperationException("KeyEventTriggerBehavior can only be attached to TextBox.");
        }

        foreach (var keyboardAccelerator in Filter)
        {
            element.KeyboardAccelerators.Add(keyboardAccelerator);
            keyboardAccelerator.Invoked += KeyboardAcceleratorOnInvoked;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject is not TextBox element)
        {
            throw new InvalidOperationException("KeyEventTriggerBehavior can only be attached to TextBox.");
        }

        foreach (var keyboardAccelerator in Filter)
        {
            element.KeyboardAccelerators.Remove(keyboardAccelerator);
            keyboardAccelerator.Invoked -= KeyboardAcceleratorOnInvoked;
        }
    }

    private void KeyboardAcceleratorOnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Interaction.ExecuteActions(this, this.Actions, args);
    }
}
