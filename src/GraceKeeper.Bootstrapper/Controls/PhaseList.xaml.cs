using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using GraceKeeper.Core;

namespace GraceKeeper.Bootstrapper.Controls;

public partial class PhaseList : UserControl
{
    public static readonly DependencyProperty PhasesProperty =
        DependencyProperty.Register(
            nameof(Phases),
            typeof(ObservableCollection<PhaseRow>),
            typeof(PhaseList),
            new PropertyMetadata(null));

    public ObservableCollection<PhaseRow> Phases
    {
        get => (ObservableCollection<PhaseRow>)GetValue(PhasesProperty);
        set => SetValue(PhasesProperty, value);
    }

    public PhaseList()
    {
        InitializeComponent();
    }
}
