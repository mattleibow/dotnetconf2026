# Part 1: What's New in .NET 11, C# 15 & .NET MAUI 11 (5–8 mins)

Open these files briefly before kicking off Prompt `01` to show why the baseline project is so small, modern, and readable.

---

## 1. `.slnx`, Central Package Management & Artifacts Output

Show the three root files that keep the repository clean:

- **[`ConferenceApp.slnx`](../ConferenceApp.slnx):** XML-based solution format—tiny, human-readable, and merge-conflict free.
- **[`Directory.Build.props`](../Directory.Build.props):** `<UseArtifactsOutput>true</UseArtifactsOutput>` places all build outputs under `/artifacts/` at the repo root so `bin/` and `obj/` folders never clutter `src/ConferenceApp/`.
- **[`Directory.Packages.props`](../Directory.Packages.props):** `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` keeps every NuGet version in one place, leaving versionless `<PackageReference>` items in [`ConferenceApp.csproj`](../src/ConferenceApp/ConferenceApp.csproj).

```xml
<Project>

    <PropertyGroup>
        <UseArtifactsOutput>true</UseArtifactsOutput>
        <NoWarn>$(NoWarn);NU1507</NoWarn>
    </PropertyGroup>

</Project>
```

---

## 2. CoreCLR, XAML Source Gen & Offline AI Assets ([`src/ConferenceApp/ConferenceApp.csproj`](../src/ConferenceApp/ConferenceApp.csproj))

In .NET 11, **CoreCLR is the default runtime** for .NET MAUI on iOS, Mac Catalyst, and Android—no legacy Mono or interpreter configuration workarounds are needed. **XAML Source Generation** (`MauiXamlInflator=SourceGen`), **XAML Incremental Hot Reload**, and **Lazy `ResourceDictionary`** are all enabled by default without extra MSBuild properties.

Point out `<EnablePreviewFeatures>true</EnablePreviewFeatures>` (which enables C# Expressions in XAML) and how both the offline conference dataset and the 22 MB ONNX model are linked directly from the repo root as `MauiAsset`s:

```xml
<PropertyGroup>
    <TargetFrameworks>net11.0-android</TargetFrameworks>
    <TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('linux'))">$(TargetFrameworks);net11.0-ios;net11.0-maccatalyst</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net11.0-windows10.0.19041.0</TargetFrameworks>
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
</PropertyGroup>

<ItemGroup>
    <!-- Conference data and ONNX embedding model, shipped with the app so it works offline -->
    <MauiAsset Include="..\..\data\conference\**" LogicalName="conference/%(RecursiveDir)%(Filename)%(Extension)" />
    <MauiAsset Include="..\..\models\all-MiniLM-L6-v2\*" LogicalName="models/all-MiniLM-L6-v2/%(Filename)%(Extension)" />
</ItemGroup>
```

---

## 3. Implicit Global XAML Namespaces ([`src/ConferenceApp/GlobalXmlns.cs`](../src/ConferenceApp/GlobalXmlns.cs))

Registering project and library namespaces once at the assembly level eliminates `xmlns:` boilerplate from every `.xaml` file in the project. Controls like `<Map />`, `<BasePage />`, custom converters, ViewModels, and Data types work directly without prefixes.

```csharp
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.AI")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Converters")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Data")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Services")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.ViewModels")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "ConferenceApp.Views")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "Microsoft.Maui.Controls.Maps", AssemblyName = "Microsoft.Maui.Controls.Maps")]
```

---

## 4. C# Expressions in XAML & AOT-Safe `RelativeSource` ([`AgendaPage.xaml`](../src/ConferenceApp/Views/AgendaPage.xaml) & [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml))

With `<EnablePreviewFeatures>true</EnablePreviewFeatures>`, XAML supports inline C# expressions—negations, ternaries, boolean `AND`, and string interpolation—eliminating `HasXyz` ViewModel properties and `InverseBooleanConverter`s. Combined with .NET 11 AOT-safe compiled `RelativeSource` bindings, templates bind directly to ancestor ViewModels without reflection.

```xml
<BasePage x:Class="ConferenceApp.Views.AgendaPage"
          x:DataType="AgendaViewModel"
          Title="Agenda">
...
<Button Grid.Column="1"
        AutomationId="favorites-filter"
        Text="{ShowFavoritesOnly ? 'Showing: Favourites' : 'Showing: All Sessions'}"
        Command="{Binding ToggleFavoritesFilterCommand}" />
...
<VerticalStackLayout Padding="0,8" Background="Transparent" AutomationId="{$'session-{Id}'}">
    <Label Text="{Binding Meta}" />
    <Label Text="{Binding Topic}" IsVisible="{!string.IsNullOrWhiteSpace(Topic)}" />
    <Label Text="{Binding Title}" FontAttributes="Bold" />
    <Label Text="{Binding Summary}" IsVisible="{!string.IsNullOrWhiteSpace(Summary)}" MaxLines="2" LineBreakMode="TailTruncation" />
    <Label Text="{Binding SpeakerNames}" />
    <VerticalStackLayout.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding OpenSessionCommand, Source={RelativeSource AncestorType={x:Type AgendaViewModel}}, x:DataType=AgendaViewModel}"
                              CommandParameter="{Binding .}" />
    </VerticalStackLayout.GestureRecognizers>
</VerticalStackLayout>
```

And in [`ChatPage.xaml`](../src/ConferenceApp/Views/ChatPage.xaml), `<CollectionView.EmptyView>` shows the starter suggestions when the conversation is empty, while `IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"` gates inputs cleanly when on-device AI is busy or unavailable:

```xml
<CollectionView Grid.Row="1" AutomationId="chat-messages" ItemsSource="{Binding Messages}">
    <CollectionView.EmptyView>
        <VerticalStackLayout Spacing="8"
                             VerticalOptions="Center"
                             IsEnabled="{string.IsNullOrEmpty(UnavailableReason) AND !IsBusy}"
                             BindableLayout.ItemsSource="{Binding Suggestions}">
            <BindableLayout.ItemTemplate>
                <DataTemplate x:DataType="x:String">
                    <Button Text="{Binding .}"
                            Command="{Binding AskSuggestionCommand, Source={RelativeSource AncestorType={x:Type ChatViewModel}}, x:DataType=ChatViewModel}"
                            CommandParameter="{Binding .}" />
                </DataTemplate>
            </BindableLayout.ItemTemplate>
        </VerticalStackLayout>
    </CollectionView.EmptyView>
```

---

## 5. Primary Constructors, Partial Properties & `BaseViewModel` / `BasePage` ([`ViewModels/`](../src/ConferenceApp/ViewModels/) & [`Views/`](../src/ConferenceApp/Views/))

[`BaseViewModel.cs`](../src/ConferenceApp/ViewModels/BaseViewModel.cs) centralizes `ConferenceDbProvider`, `UserPreferencesService`, `IDispatcher`, `WeakReferenceMessenger.Default.RegisterAll(this)`, `SelectedCity`, `PickCityCommand`, and `LoadCommand`. That lets every page ViewModel use a C# primary constructor and `[ObservableProperty]` partial properties with zero boilerplate:

```csharp
public partial class SpeakersViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher) : BaseViewModel(dbProvider, preferences, dispatcher)
{
    public ObservableCollection<Speaker> Speakers { get; } = [];

    protected override async Task OnLoadAsync()
    {
        if (SelectedCity is not { } city)
            return;

        var db = await DbProvider.GetDbAsync();
        Speakers.Clear();
        foreach (var speaker in db.Speakers.Where(s => s.CityIds.Contains(city.Id)))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
```

And [`BasePage.cs`](../src/ConferenceApp/Views/BasePage.cs) wires `BindingContext` and `OnAppearing() => ViewModel.LoadCommand.Execute(null)` so each `.xaml.cs` code-behind is just:

```csharp
public partial class SpeakersPage : BasePage
{
    public SpeakersPage(SpeakersViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
```

---

## 6. Single-File C# Scripts (`dotnet run file.cs`) ([`data/ImportConference.cs`](../data/ImportConference.cs) & [`models/DownloadModels.cs`](../models/DownloadModels.cs))

Repo utilities don't need separate `.csproj` files. Both the conference importer (`data/ImportConference.cs`) and the HuggingFace ONNX model verifier (`models/DownloadModels.cs`) are standalone `.cs` scripts targeting `.NET 11` and `C# 15` with `#:property` and `#:package` directives:

```csharp
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:package AngleSharp@1.1.2
```

---

## 7. Clean Startup, DevFlow & Data Layer ([`MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs), [`App.xaml.cs`](../src/ConferenceApp/App.xaml.cs) & [`Data/`](../src/ConferenceApp/Data/))

`MauiProgram.cs` registers the data provider, app services, a single `builder.AddConferenceAI()` call, separate `AddTransient<Page>().AddTransient<ViewModel>()` pairs, and the MAUI DevFlow agent + AI debug logging in `#if DEBUG`:

```csharp
builder.Services.AddSingleton<ConferenceDbProvider>();
builder.Services.AddSingleton<UserPreferencesService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<ChatService>();

builder.AddConferenceAI();
```

In [`App.xaml.cs`](../src/ConferenceApp/App.xaml.cs), `InitializeAIAsync` indexes session vectors first with `SessionIndexingService` and then starts background LLM enrichment with `SessionEnrichmentService`:

```csharp
private static async Task InitializeAIAsync(
    SessionIndexingService indexing,
    SessionEnrichmentService enrichment)
{
    await indexing.StartAsync();
    await enrichment.StartAsync();
}
```

In [`src/ConferenceApp/Data/`](../src/ConferenceApp/Data/), [`ConferenceDbProvider.cs`](../src/ConferenceApp/Data/ConferenceDbProvider.cs) is a 6-line on-demand provider (`GetDbAsync()`) over [`ConferenceDb.cs`](../src/ConferenceApp/Data/ConferenceDb.cs), which loads `conference.json` once, caches all Markdown descriptions (`session.Description`, `speaker.Bio`, `venue.Description`), and links navigation properties (`city.Venue`, `session.City`, `session.Speakers`). User state (selected city and favourites) lives separately in [`UserPreferencesService.cs`](../src/ConferenceApp/Services/UserPreferencesService.cs).

Next: [**Part 2 — Agents Building Apps (`02-agents-building-apps.md`)**](02-agents-building-apps.md)
