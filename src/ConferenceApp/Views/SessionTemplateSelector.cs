using ConferenceApp.ViewModels;
namespace ConferenceApp.Views;

public sealed class SessionTemplateSelector : DataTemplateSelector
{
    public DataTemplate Talk { get; set; } = null!;
    public DataTemplate General { get; set; } = null!;
    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item is SessionItem { IsGeneral: true } ? General : Talk;
}
