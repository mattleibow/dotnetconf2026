# Bots Just Wanna Browse

You wanted some web data. Your HTTP request returned an empty JavaScript shell. You added a browser. Now you’re managing processes, preserving logins, handling timeouts and wondering where all your memory went. And the site still thinks you’re a bot.

AI agents run into the same obstacles, with the added liability of burning through tool calls and tokens along the way.

This talk follows my attempt to put that complexity behind a small, self-hosted browser API. Send a URL, add any wait or click actions, and get back rendered HTML, cookies and headers. Named sessions preserve browser state across requests; structured errors tell the caller when things go wrong.

I’ll demonstrate a scraper using the service, then connect an agent to it through MCP. Along the way, we’ll examine the awkward parts: browser capacity, session management and anti-bot challenges.

You’ll see how the pieces fit together and have a clearer basis for deciding whether to build, buy or stick with plain HTTP.
