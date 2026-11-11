using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SessionPage : ContentPage
{
    public SessionPage(SessionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var wide = width >= 800;
        if (DetailGrid.ColumnDefinitions.Count == (wide ? 2 : 1))
            return;
        DetailGrid.ColumnDefinitions.Clear();
        DetailGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (wide)
            DetailGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetRow(AbstractSection, wide ? 0 : 1);
        Grid.SetColumn(AbstractSection, wide ? 1 : 0);
    }
}
