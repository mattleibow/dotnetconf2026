# The Agent Is Just a Loop: Building AI Agents in C# With Microsoft Agent Framework

Everyone is talking about AI agents, but what *is* one, in code you can actually read? In this session, we find out the honest way: by writing one ourselves. A couple of dozen

lines of C#: the model asks for a tool, our code runs it, the answer goes back in, and round it goes until the model has what it needs. That loop is the whole secret.

Then we hand the loop to Microsoft Agent Framework, the successor to Semantic Kernel and AutoGen, and start adding layers. A ready-made MCP connector lets our agent explore a

real music-store database and write its own SQL. A session gives it a memory, so follow-up questions make sense. A second agent checks the first one's numbers, and

catches the kind of messy data that fools people too. Finally we unplug from the cloud and run the same agent on a laptop by changing one line.

Almost everything is live code, and every step prints the loop as it runs, so you can watch the agent think: each trip to the model, each tool it asks for, each query it writes. You leave with a mental model that works for any agent framework in any language, a sample repo you can run at home, and an honest answer to the question nobody asks enough: when *shouldn't* you use an agent?

If you can read C#, you can follow along. No AI experience needed.
