---
description: Add keyword search to the agenda
---

Add a search box to the agenda.

Put the search in one small service with `SearchSessionsAsync(query)`, so other features can use it later. For now, a case-insensitive match over the title, abstract, speaker names and track is enough, limited to the selected city. Show the matching bit of text under each result, and restore the normal agenda when the box is empty.

Keep it simple; we'll make it smarter in the next steps.

Use DevFlow to try "maui", a speaker's name, and a query with no results.
