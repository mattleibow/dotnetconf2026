---
description: Ground the assistant in the full abstracts with simple RAG
---

The assistant only sees one-line summaries. Add simple RAG with a few lines of middleware using `ChatClientBuilder.Use(...)`:

- Before each call, search with the latest question plus the assistant's previous answer, and add the top 3 matches as a context message just before the question.
- Don't keep that context in the chat history, and don't change the system instructions.

Use DevFlow to ask "Which talks are about keeping AI costs down?" and then "Tell me more about the second one".
