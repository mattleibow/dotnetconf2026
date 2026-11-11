# .NET Conf South Africa companion

The app bundles the `data/conference/` snapshot under `Resources/Raw/conference/`. Agenda, speaker, venue and sponsor content is available offline; favourites and the selected city are stored on the device. Finding the nearest city is optional and asks for location permission only after explaining its use.

Search builds an in-memory index of session abstracts using on-device sentence embeddings: Apple's NaturalLanguage model on iOS and Mac Catalyst, and the bundled all-MiniLM-L6-v2 ONNX model on Android and Windows. Set `MauiProgram.UseOnnxOnApple` to `true` to compare ONNX on Apple devices. Exact keyword matches appear first, followed by up to five semantic matches ranked by their best abstract chunk. Semantic matches are ranked without a relevance cutoff, so even unrelated queries can return results. If the Apple sentence model is unavailable, the app shows that it is using keyword search instead. The index is rebuilt each time the app starts and no search content is sent to a service.

For indexing, the bundled session Markdown is copied into app data and processed by a Data Ingestion pipeline with a Markdown reader, two-sentence chunker and in-memory vector-store writer. The copies are refreshed from the snapshot whenever the index is rebuilt.

Build one target at a time with a compatible .NET 11 SDK and MAUI workload:

```sh
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-maccatalyst
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-ios
dotnet build src/ConferenceApp/ConferenceApp.csproj -f net11.0-android
```

Debug builds include the MAUI DevFlow agent. When updating the conference snapshot, copy `data/conference/` into `Resources/Raw/conference/` again.

The assistant uses `Microsoft.Maui.Essentials.AI` and `IChatClient` with Apple Intelligence on supported iOS and Mac Catalyst devices. The selected city's compact agenda, current time, favourites and relevant session excerpts stay on device. Source-generated tools let the model look up conference information, open session, speaker, sponsor and venue pages, and request a favourite change. Favourite changes require explicit approval in the assistant; denying or cancelling leaves them unchanged. Session titles reproduced in an answer link to their detail pages. Answers can be incomplete or inaccurate; check the agenda. Other platforms show an unavailable message; no models are downloaded by the app. In Debug builds, the assistant can use a chosen time on the selected event day.
