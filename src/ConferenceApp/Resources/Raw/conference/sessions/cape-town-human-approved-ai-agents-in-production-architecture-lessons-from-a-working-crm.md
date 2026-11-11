# Human-approved AI agents in production: architecture lessons from a working CRM

Naya CRM's AI assistant researches sales leads and drafts email, WhatsApp and SMS outreach, and nothing reaches a customer until a person approves it. This session walks through the architecture that makes that safe and affordable: approval gates stored as data, routing between cheap and capable models with metered credits, MCP tools with the smallest possible permissions, POPIA erasure and opt-outs, and the rules of Meta's WhatsApp Business Platform.

You will leave with three patterns:

1. Human approval as a status in the data model, with one send path that every sender goes through.

2. Each AI task routed to the cheapest model that does it well, falling back only on capacity errors, with every call metered against a per-tenant budget.

3. MCP tools, POPIA erasure and WhatsApp's 24-hour window treated as hard constraints in code.

The production system is TypeScript, and I say so up front: every section ends with the .NET building blocks you would use for the same job (EF Core, BackgroundService, Microsoft.Extensions.AI, the MCP C# SDK, minimal APIs). This is an architecture talk, not a product demo.
