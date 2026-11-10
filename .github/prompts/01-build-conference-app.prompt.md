---
description: Build the conference app from the demo data
---

Build a beautiful .NET MAUI 11 app for this conference using the data in `data/conference/`. Read `data/README.md` first to see how it's laid out. Don't scrape the website; everything is already here, and the app must work offline.

Put the app in `src/ConferenceApp/` and copy the data into its `Resources/Raw/conference/` so it ships as raw assets.

The app should have:

- the agenda, with speakers
- a city picker, since the event runs in three cities. Offer to find the nearest city with location (explain why before asking), but always allow picking manually, and remember the choice
- session and speaker details
- favourite sessions, saved on the device
- sponsors
- the venue

Make it look polished and on-brand, and celebrate .NET 11 and MAUI 11. Keep the code simple and clean: this is a demo, not a product. No AI yet.

Use MAUI DevFlow to run and check the app as you go, and take screenshots. If you can't run it, tell me rather than assuming it works.
