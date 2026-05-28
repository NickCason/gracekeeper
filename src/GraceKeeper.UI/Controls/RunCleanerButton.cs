using System.Windows;
using System.Windows.Controls.Primitives;
using GraceKeeper.UI.ViewModels;

namespace GraceKeeper.UI.Controls;

public class RunCleanerButton : ButtonBase
{
    static RunCleanerButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RunCleanerButton),
            new FrameworkPropertyMetadata(typeof(RunCleanerButton)));
    }

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State), typeof(CleanerButtonState), typeof(RunCleanerButton),
            new PropertyMetadata(CleanerButtonState.Idle, OnStateChanged));

    public CleanerButtonState State
    {
        get => (CleanerButtonState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateVisualState(useTransitions: false);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RunCleanerButton)d).UpdateVisualState(useTransitions: true);
    }

    private void UpdateVisualState(bool useTransitions)
    {
        var stateName = State switch
        {
            CleanerButtonState.Running => "Running",
            CleanerButtonState.Done => "Done",
            CleanerButtonState.Failed => "Failed",
            _ => "Idle",
        };
        VisualStateManager.GoToState(this, stateName, useTransitions);
    }
}
