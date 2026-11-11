# Don't Ask, Watch: Real-Time Data and AI with .NET 11 and Foundry Local

Most AI demos wait for someone to ask a question. But a lot of real data doesn't wait: sales, sensor readings, logs and payments keep arriving, and someone has to notice when
something matters. So why not just point an AI at the stream?

In this session we try exactly that, and watch it fall over. Latency climbs, the queue backs up, and the on-screen bill counter gets painful. Then we fix it the way stream
processing has always worked: channels, windows and simple rules in plain .NET 11 do the watching, and the model is only called for the handful of moments that actually need explaining. Model calls drop from hundreds a minute to a couple, and the explanations get *better*.

Along the way you'll see `System.Threading.Channels`, `IAsyncEnumerable`, backpressure, and a live dashboard fed by Server-Sent Events, now described in OpenAPI 3.2 in
.NET 11. It all runs on one laptop, with a local model served by Microsoft's Foundry Local, no cloud required. You leave with a pattern that works with any model and any message broker, and a clear sense of where AI adds value and where it only adds cost.
