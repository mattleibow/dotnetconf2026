# .NET Conf South Africa companion

The app bundles the `data/conference/` snapshot under `Resources/Raw/conference/`. Agenda, speaker, venue and sponsor content is available offline; favourites and the selected city are stored on the device. Finding the nearest city is optional and asks for location permission only after explaining its use.

Build one target at a time with a compatible .NET 11 SDK and MAUI workload:

```sh
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-maccatalyst
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-ios
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-android
```

Debug builds include the MAUI DevFlow agent. When updating the conference snapshot, copy `data/conference/` into `Resources/Raw/conference/` again.
