﻿using H.NotifyIcon.Core;

namespace H.NotifyIcon;

/// <inheritdoc/>
public partial class TaskbarIcon
{
    #region Properties

    #region ToolTipText

    /// <summary>Identifies the <see cref="ToolTipText"/> dependency property.</summary>
    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register(
            nameof(ToolTipText),
            typeof(string),
            typeof(TaskbarIcon),
            new PropertyMetadata(string.Empty, (d, e) => ((TaskbarIcon)d).OnToolTipTextPropertyChanged(e)));

    /// <summary>
    /// A property wrapper for the <see cref="ToolTipTextProperty"/>
    /// dependency property:<br/>
    /// A tooltip text that is being displayed if no custom <see cref="ToolTip"/>
    /// was set or if custom tooltips are not supported.
    /// </summary>
    [Category(CategoryName)]
    [Description("Alternative to a fully blown ToolTip, which is only displayed on Vista and above.")]
    public string ToolTipText
    {
        get { return (string)GetValue(ToolTipTextProperty); }
        set { SetValue(ToolTipTextProperty, value); }
    }

    /// <summary>
    /// Handles changes of the <see cref="ToolTipTextProperty"/> dependency property. As
    /// WPF internally uses the dependency property system and bypasses the
    /// <see cref="ToolTipText"/> property wrapper, updates of the property's value
    /// should be handled here.
    /// </summary>
    /// <param name="e">Provides information about the updated property.</param>
    private void OnToolTipTextPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        //do not touch tooltips if we have a custom tooltip element
        if (TrayToolTip == null)
        {
            var currentToolTip = TrayToolTipResolved;
            if (currentToolTip == null)
            {
                //if we don't have a wrapper tooltip for the tooltip text, create it now
                CreateCustomToolTip();
            }
            else
            {
                //if we have a wrapper tooltip that shows the old tooltip text, just update content
                currentToolTip.Content = e.NewValue;
            }
        }

        WriteToolTipSettings();
    }

    #endregion

    #region TrayToolTip

    /// <summary>Identifies the <see cref="TrayToolTip"/> dependency property.</summary>
    public static readonly DependencyProperty TrayToolTipProperty =
        DependencyProperty.Register(
            nameof(TrayToolTip),
            typeof(UIElement),
            typeof(TaskbarIcon),
            new PropertyMetadata(null, (d, e) => ((TaskbarIcon)d).OnTrayToolTipPropertyChanged(e)));

    /// <summary>
    /// A property wrapper for the <see cref="TrayToolTipProperty"/>
    /// dependency property:<br/>
    /// A custom UI element that is displayed as a tooltip if the user hovers over the taskbar icon.
    /// Works only with Vista and above. Accordingly, you should make sure that
    /// the <see cref="ToolTipText"/> property is set as well.
    /// </summary>
    [Category(CategoryName)]
    [Description("Custom UI element that is displayed as a tooltip. Only on Vista and above")]
    public UIElement? TrayToolTip
    {
        get { return (UIElement?)GetValue(TrayToolTipProperty); }
        set { SetValue(TrayToolTipProperty, value); }
    }


    /// <summary>
    /// Handles changes of the <see cref="TrayToolTipProperty"/> dependency property. As
    /// WPF internally uses the dependency property system and bypasses the
    /// <see cref="TrayToolTip"/> property wrapper, updates of the property's value
    /// should be handled here.
    /// </summary>
    /// <param name="e">Provides information about the updated property.</param>
    private void OnTrayToolTipPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        //recreate tooltip control
        CreateCustomToolTip();

#if HAS_WPF
        if (e.OldValue != null)
        {
            //remove the taskbar icon reference from the previously used element
            SetParentTaskbarIcon((DependencyObject)e.OldValue, null);
        }

        if (e.NewValue != null)
        {
            //set this taskbar icon as a reference to the new tooltip element
            SetParentTaskbarIcon((DependencyObject)e.NewValue, this);
        }
#endif

        //update tooltip settings - needed to make sure a string is set, even
        //if the ToolTipText property is not set. Otherwise, the event that
        //triggers tooltip display is never fired.
        WriteToolTipSettings();
    }

    #endregion

    #region TrayToolTipResolved

    /// <summary>Identifies the <see cref="TrayToolTipResolved"/> dependency property.</summary>
    public static readonly DependencyProperty TrayToolTipResolvedProperty
        = DependencyProperty.Register(
            nameof(TrayToolTipResolved),
            typeof(ToolTip),
            typeof(TaskbarIcon),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets the TrayToolTipResolved property. Returns 
    /// a <see cref="ToolTip"/> control that was created
    /// in order to display either <see cref="TrayToolTip"/>
    /// or <see cref="ToolTipText"/>.
    /// </summary>
    [Category(CategoryName)]
    [Browsable(true)]
    [Bindable(true)]
    public ToolTip? TrayToolTipResolved => (ToolTip?)GetValue(TrayToolTipResolvedProperty);

    /// <summary>
    /// Provides a secure method for setting the <see cref="TrayToolTipResolved"/>
    /// property.  
    /// </summary>
    /// <param name="value">The new value for the property.</param>
    protected void SetTrayToolTipResolved(ToolTip? value)
    {
        SetValue(TrayToolTipResolvedProperty, value);
    }

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Creates a <see cref="ToolTip"/> control that either
    /// wraps the currently set <see cref="TrayToolTip"/>
    /// control or the <see cref="ToolTipText"/> string.<br/>
    /// If <see cref="TrayToolTip"/> itself is already
    /// a <see cref="ToolTip"/> instance, it will be used directly.
    /// </summary>
    /// <remarks>We use a <see cref="ToolTip"/> rather than
    /// <see cref="Popup"/> because there was no way to prevent a
    /// popup from causing cyclic open/close commands if it was
    /// placed under the mouse. ToolTip internally uses a Popup of
    /// its own, but takes advance of Popup's internal <see cref="UIElement.IsHitTestVisible"/>
    /// property which prevents this issue.</remarks>
    private void CreateCustomToolTip()
    {
        // check if the item itself is a tooltip
        var tt = TrayToolTip as ToolTip;

        if (tt == null && TrayToolTip != null)
        {
            // create an invisible wrapper tooltip that hosts the UIElement
            tt = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                // do *not* set the placement target, as it causes the popup to become hidden if the
                // TaskbarIcon's parent is hidden, too. At runtime, the parent can be resolved through
                // the ParentTaskbarIcon attached dependency property:
                // PlacementTarget = this;

                // make sure the tooltip is invisible
#if HAS_WPF
                HasDropShadow = false,
                Background = System.Windows.Media.Brushes.Transparent,
                // setting the
                StaysOpen = true,
#endif
                BorderThickness = new Thickness(0),
                Content = TrayToolTip,
            };
        }
        else if (tt == null && !string.IsNullOrEmpty(ToolTipText))
        {
            // create a simple tooltip for the ToolTipText string
            tt = new ToolTip
            {
                Content = ToolTipText
            };
        }

        // the tooltip explicitly gets the DataContext of this instance.
        // If there is no DataContext, the TaskbarIcon assigns itself
        if (tt != null)
        {
            UpdateDataContext(tt, null, DataContext);
        }

        // store a reference to the used tooltip
        SetTrayToolTipResolved(tt);
    }

    /// <summary>
    /// Sets tooltip settings for the class depending on defined
    /// dependency properties and OS support.
    /// </summary>
    private void WriteToolTipSettings()
    {
        var text = ToolTipText;
        if (TrayIcon.Version == IconVersion.Vista)
        {
            // we need to set a tooltip text to get tooltip events from the
            // taskbar icon
            if (string.IsNullOrEmpty(text) && TrayToolTipResolved != null)
            {
                // if we have not tooltip text but a custom tooltip, we
                // need to set a dummy value (we're displaying the ToolTip control, not the string)
                text = "ToolTip";
            }
        }

        if (!TrayIcon.IsCreated)
        {
            return;
        }

        TrayIcon.UpdateToolTip(text);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Displays a custom tooltip, if available. This method is only
    /// invoked for Windows Vista and above.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="visible">Whether to show or hide the tooltip.</param>
    private void OnToolTipChange(object? sender, bool visible)
    {
        // if we don't have a tooltip, there's nothing to do here...
        if (TrayToolTipResolved == null)
        {
            return;
        }

        if (visible)
        {
            if (IsPopupOpen)
            {
                // ignore if we are already displaying something down there
                return;
            }

#if HAS_WPF
            var args = RaisePreviewTrayToolTipOpenEvent();
            if (args.Handled)
            {
                return;
            }
#endif

            TrayToolTipResolved.IsOpen = true;

#if HAS_WPF
            // raise attached event first
            if (TrayToolTip != null)
            {
                RaiseToolTipOpenedEvent(TrayToolTip);
            }

            // bubble routed event
            RaiseTrayToolTipOpenEvent();
#endif
        }
        else
        {
#if HAS_WPF
            var args = RaisePreviewTrayToolTipCloseEvent();
            if (args.Handled)
            {
                return;
            }

            // raise attached event first
            if (TrayToolTip != null)
            {
                RaiseToolTipCloseEvent(TrayToolTip);
            }
#endif

            TrayToolTipResolved.IsOpen = false;

#if HAS_WPF
            // bubble event
            RaiseTrayToolTipCloseEvent();
#endif
        }
    }

    #endregion
}