using System;
using CommunityToolkit.WinUI.Controls;
using Jellyfin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Jellyfin.Views;

/// <summary>
/// Represents a user interface control that allows users to select a culture or language for the application.
/// </summary>
/// <remarks>This control is typically used in applications that support localization, enabling users to change
/// the application's display language at runtime. The control is bound to a view model that manages the available
/// cultures and the current selection.</remarks>
public sealed partial class CultureSelector : UserControl
{
    /// <summary>
    /// Identifies the Orientation dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Dock), typeof(CultureSelector), new PropertyMetadata(Dock.Right));

    /// <summary>
    /// Initializes a new instance of the <see cref="CultureSelector"/> class.
    /// </summary>
    /// <remarks>This constructor sets up the CultureSelector control and assigns its data context to an
    /// instance of CultureSelectorViewModel obtained from the application's service provider. This enables data binding
    /// and interaction with the view model in accordance with the MVVM pattern.</remarks>
    public CultureSelector()
    {
        InitializeComponent();
        DataContext = App.Current?.Services.GetRequiredService<CultureSelectorViewModel>();
    }

    /// <summary>
    /// Gets or sets the orientation of the language selector.
    /// </summary>
    public Dock Orientation
    {
        get { return (Dock)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }
}
