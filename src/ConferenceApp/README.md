# .NET Conf South Africa companion

The app bundles the `data/conference/` snapshot under `Resources/Raw/conference/`. Agenda, speaker, venue and sponsor content is available offline; favourites and the selected city are stored on the device. Finding the nearest city is optional and asks for location permission only after explaining its use.

On iOS and Mac Catalyst, search builds an in-memory index of session abstracts using Apple's on-device NaturalLanguage sentence embeddings. Exact keyword matches appear first, followed by semantic matches; other platforms use keyword search only. If the Apple sentence model is unavailable, the app shows that it is using keyword search instead. The index is rebuilt each time the app starts and no search content is sent to a service.

Build one target at a time with a compatible .NET 11 SDK and MAUI workload:

```sh
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-maccatalyst
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-ios
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-android
```

Debug builds include the MAUI DevFlow agent. When updating the conference snapshot, copy `data/conference/` into `Resources/Raw/conference/` again.
