---
description: Create the conference app from the MAUI template
---

Create a .NET MAUI 11 app for this conference in `src/ConferenceApp/` using `dotnet new maui`. Make sure the untouched template builds and runs before changing anything.

Then turn it into a beautiful companion app using the data in `data/conference/` (see `data/README.md`). Copy that folder into `Resources/Raw/conference/` so it ships with the app. Don't scrape the website; the app must work offline.

The app should have:

- the agenda, with speakers
- a city picker, since the event runs in three cities. Offer to find the nearest city with location (explain why before asking), but always allow picking manually, and remember the choice
- session and speaker details
- favourite sessions, saved on the device
- sponsors
- the venue

It should look like a professional, shipping conference app from the start: on-brand, with real speaker photos and sponsor logos, and a layout that works on a phone in portrait and in a big desktop window. Follow `docs/design.md` exactly, starting with the theme, and celebrate .NET 11 and MAUI 11. Keep the code simple and clean: this is a demo, not a product. No AI yet.

Add MAUI DevFlow and use it to run and check the app as you go, with screenshots on both a phone and a desktop window. If you can't run it, tell me rather than assuming it works.
