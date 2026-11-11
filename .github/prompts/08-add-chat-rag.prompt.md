---
description: Ground the assistant in the full abstracts with simple RAG
---

The assistant only sees one-line summaries. Add simple RAG with a few lines of middleware using `ChatClientBuilder.Use(...)`:

- Before each call, use our existing search service for the selected city, searching with the latest question plus the assistant's previous answer. Add the top 3 sessions (title, time, speakers and abstract) as a context message just before the question.
- Don't keep that context in the chat history, and don't change the system instructions.

Use DevFlow in Durban to ask "Is there a talk about keeping AI costs down?" and then "Who's giving it, and when?". If the right talk doesn't come back, fix the search rather than the prompt; the ONNX model from step 4 ranks better than Apple's.
