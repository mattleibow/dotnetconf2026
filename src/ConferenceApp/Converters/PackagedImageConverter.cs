using System.Globalization;

namespace ConferenceApp.Converters;

public sealed class PackagedImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } path
            ? ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync($"conference/{path}"))
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
