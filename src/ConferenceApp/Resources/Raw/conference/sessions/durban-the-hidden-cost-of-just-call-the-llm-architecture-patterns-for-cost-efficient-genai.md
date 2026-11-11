# The Hidden Cost of “Just Call the LLM”: Architecture Patterns for Cost-Efficient GenAI

Calling a large language model from your app takes one line of code. Paying for it at scale is another story. Generative AI services are billed per token, and in cloud-native applications that scale automatically, small design decisions quickly turn into large and unpredictable bills.

This session draws on a systematic review of recent research and provider evidence to show where generative AI costs actually come from, and which architecture patterns reduce them in production: model routing and cascading, prompt and response caching, batch APIs for workloads that can wait, context management for long conversations, loading only the tools an agent needs, and budget guardrails that stop runaway agent loops.

Just as important, we’ll look at the trade-offs. Cost saving touches latency or output quality somewhere: caches can return wrong answers, compression can drop key facts, and spending caps can lower quality. You’ll leave with a knowledge of choosing the right patterns for your workload, so your AI features stay affordable as they grow.
