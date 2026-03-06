// Standalone demo: shows jdai's markdown and diff rendering without needing a live model.
// Run with: dotnet run --project docs/demo/
using JD.AI.Rendering;
using Spectre.Console;

// ── Welcome banner (mirrors jdai startup) ────────────────────────────────────
ChatRenderer.ApplyTheme(JD.AI.Core.Config.TuiTheme.DefaultDark);
AnsiConsole.Write(
    new Panel(
        new Markup(
            "[bold]jdai[/] — Semantic Kernel TUI Agent\n" +
            "Provider: [#67d8ef]Claude Code[/] | Model: [green]claude-sonnet-4-5[/] | Total models: 147\n" +
            "Type [dim]/help[/] for commands, [dim]/quit[/] to exit."))
    .Border(BoxBorder.Rounded)
    .Header("[bold #4ea1ff]Welcome[/]")
    .Padding(1, 0));
AnsiConsole.WriteLine();

// ── Simulate user prompt ──────────────────────────────────────────────────────
AnsiConsole.Markup("[bold #67d8ef]>[/] ");
AnsiConsole.MarkupLine("[#9aa0a6]Explain async/await in C# with an example, a comparison table, and key rules.[/]");
AnsiConsole.WriteLine();

// ── Simulate streaming indicator ─────────────────────────────────────────────
Thread.Sleep(400);

// ── Render sample AI markdown response ───────────────────────────────────────
const string MarkdownResponse = """
## Async / Await in C#

Async programming lets you write **non-blocking** code that looks sequential.
The `async` and `await` keywords work together: `async` marks a method as
asynchronous, while `await` suspends execution until the awaited task completes —
*without* blocking the calling thread.

### Example

```csharp
public async Task<string> FetchDataAsync(string url)
{
    using var http = new HttpClient();
    // Suspends here; thread is freed for other work
    var response = await http.GetStringAsync(url);
    return response;
}
```

### When to use async

- I/O-bound operations (network, disk, database)
- Long-running tasks that would otherwise block the UI thread
- Any API that exposes a `*Async` counterpart

### Sync vs Async comparison

| Aspect | Synchronous | Async / Await |
|---|---|---|
| Thread usage | Blocks caller thread | Frees thread during I/O |
| Code style | Sequential, simple | Sequential *looking*, non-blocking |
| Error handling | `try/catch` | `try/catch` (same!) |
| Best for | CPU-bound, trivial | I/O-bound, scalable services |

> **Key rule:** avoid `async void` (except event handlers) — prefer `async Task`.
> Use `/help` to explore more commands, or `/model` to switch providers.
""";

MarkdownRenderer.Render(MarkdownResponse);

// ── Metrics footer ────────────────────────────────────────────────────────────
AnsiConsole.MarkupLine("[dim]  ── 1.4s │ 312 tokens │ 2.1 KB │ 223.0 tok/s ──[/]");
AnsiConsole.WriteLine();

// ── Simulate second turn: diff output ────────────────────────────────────────
AnsiConsole.Markup("[bold #67d8ef]>[/] ");
AnsiConsole.MarkupLine("[#9aa0a6]Show the diff for adding retry logic to FetchDataAsync.[/]");
AnsiConsole.WriteLine();

Thread.Sleep(300);

const string DiffResponse = """
--- a/Services/DataService.cs
+++ b/Services/DataService.cs
@@ -1,8 +1,19 @@
 public async Task<string> FetchDataAsync(string url)
 {
     using var http = new HttpClient();
-    var response = await http.GetStringAsync(url);
-    return response;
+    const int maxRetries = 3;
+    for (var attempt = 0; attempt < maxRetries; attempt++)
+    {
+        try
+        {
+            return await http.GetStringAsync(url);
+        }
+        catch (HttpRequestException) when (attempt < maxRetries - 1)
+        {
+            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
+        }
+    }
+    throw new InvalidOperationException("All retries exhausted.");
 }
""";

DiffRenderer.Render(DiffResponse);

AnsiConsole.MarkupLine("[dim]  ── 0.9s │ 187 tokens │ 1.3 KB │ 207.7 tok/s ──[/]");
AnsiConsole.WriteLine();

// ── Slash command coloring example ───────────────────────────────────────────
AnsiConsole.Markup("[bold #67d8ef]>[/] ");
AnsiConsole.MarkupLine("[#9aa0a6]What commands do I have?[/]");
AnsiConsole.WriteLine();

Thread.Sleep(300);

const string SlashResponse = """
Here are your most useful commands:

- /help — show all available commands
- /model — switch AI provider or model
- /provider — manage configured providers
- /diff — show uncommitted git changes
- /workflow — create and run structured workflows
- /skills — manage loaded skills
- /clear — clear conversation history

Use /quit to exit jdai at any time.
""";

MarkdownRenderer.Render(SlashResponse);
AnsiConsole.MarkupLine("[dim]  ── 0.3s │ 89 tokens │ 0.6 KB │ 296.7 tok/s ──[/]");
AnsiConsole.WriteLine();

AnsiConsole.MarkupLine("[bold #67d8ef]>[/] [dim]_[/]");
