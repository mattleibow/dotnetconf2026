# MCP is not a magic word: What it means for Software Architecture

Model Context Protocol (MCP) has quickly become one of the most talked-about ways to connect AI applications with external tools, services, and data. It promises a standard way for AI systems to discover and interact with capabilities outside the model, but introducing MCP does not make the underlying engineering problems disappear.

An MCP server still sits between systems, so It still needs authentication, authorization, validation, observability, failure handling, and sensible boundaries. The difference is that the consumer may now be an AI application deciding which tool to call and what information to provide. This session takes MCP beyond the "build your first MCP server" tutorial and looks at it from a software architecture perspective. We will explore where MCP fits in an application, how MCP servers relate to traditional APIs and services, what changes when AI becomes the consumer of a capability, and which existing engineering practices still apply.

Attendees will leave with a practical mental model for deciding where MCP belongs in their systems and how to introduce it without turning their architecture into a collection of loosely controlled AI tools.
