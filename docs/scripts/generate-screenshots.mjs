#!/usr/bin/env node
// Generates terminal-styled PNG screenshots for documentation.
// Usage: node docs/scripts/generate-screenshots.mjs
// Requires: npx playwright (globally available)

import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const imagesDir = join(__dirname, '..', 'images');
mkdirSync(imagesDir, { recursive: true });

// Terminal color palette (Dracula theme)
const colors = {
  bg: '#282a36', fg: '#f8f8f2', comment: '#6272a4',
  cyan: '#8be9fd', green: '#50fa7b', orange: '#ffb86c',
  pink: '#ff79c6', purple: '#bd93f9', red: '#ff5555',
  yellow: '#f1fa8c', selection: '#44475a',
  // Extra UI colors
  dimWhite: '#adb5bd', brightWhite: '#ffffff',
  border: '#44475a', titleBg: '#44475a',
};

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// Inline ANSI-like markup: [color]text[/color]
function renderLine(line) {
  return line
    .replace(/\[green\](.*?)\[\/green\]/g, `<span style="color:${colors.green}">$1</span>`)
    .replace(/\[red\](.*?)\[\/red\]/g, `<span style="color:${colors.red}">$1</span>`)
    .replace(/\[cyan\](.*?)\[\/cyan\]/g, `<span style="color:${colors.cyan}">$1</span>`)
    .replace(/\[yellow\](.*?)\[\/yellow\]/g, `<span style="color:${colors.yellow}">$1</span>`)
    .replace(/\[purple\](.*?)\[\/purple\]/g, `<span style="color:${colors.purple}">$1</span>`)
    .replace(/\[pink\](.*?)\[\/pink\]/g, `<span style="color:${colors.pink}">$1</span>`)
    .replace(/\[orange\](.*?)\[\/orange\]/g, `<span style="color:${colors.orange}">$1</span>`)
    .replace(/\[dim\](.*?)\[\/dim\]/g, `<span style="color:${colors.comment}">$1</span>`)
    .replace(/\[bold\](.*?)\[\/bold\]/g, `<span style="font-weight:700">$1</span>`)
    .replace(/\[b\](.*?)\[\/b\]/g, `<span style="font-weight:700">$1</span>`);
}

function buildHtml(title, lines, { width = 900, promptChar = '>' } = {}) {
  const content = lines.map(l => renderLine(escapeHtml(l))).join('\n');
  return `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<style>
  @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;700&display=swap');
  * { margin:0; padding:0; box-sizing:border-box; }
  body { background:${colors.bg}; padding:24px; display:flex; justify-content:center; }
  .terminal { width:${width}px; background:${colors.bg}; border-radius:12px;
    border:1px solid ${colors.border}; overflow:hidden;
    box-shadow: 0 8px 32px rgba(0,0,0,0.4); }
  .titlebar { background:${colors.titleBg}; padding:10px 16px; display:flex;
    align-items:center; gap:8px; }
  .dot { width:12px; height:12px; border-radius:50%; }
  .dot-red { background:#ff5f56; }
  .dot-yellow { background:#ffbd2e; }
  .dot-green { background:#27c93f; }
  .title-text { color:${colors.comment}; font-family:'JetBrains Mono',monospace;
    font-size:12px; margin-left:8px; }
  .content { padding:16px 20px; font-family:'JetBrains Mono',monospace;
    font-size:13px; line-height:1.6; color:${colors.fg}; white-space:pre-wrap;
    word-break:break-word; }
</style></head><body>
<div class="terminal">
  <div class="titlebar">
    <div class="dot dot-red"></div>
    <div class="dot dot-yellow"></div>
    <div class="dot dot-green"></div>
    <span class="title-text">${escapeHtml(title)}</span>
  </div>
  <div class="content">${content}</div>
</div>
</body></html>`;
}

// ── Screenshot definitions ──────────────────────────────────────

const screenshots = [
  {
    name: 'demo-startup',
    title: 'jdai — Provider Detection',
    lines: [
      '[dim]Detecting providers...[/dim]',
      '  [green]✅[/green] [bold]Claude Code[/bold]: Authenticated',
      '  [green]✅[/green] [bold]GitHub Copilot[/bold]: Authenticated — 3 model(s)',
      '  [green]✅[/green] [bold]Ollama[/bold]: 59 model(s) available',
      '  [dim]Loaded skills from ~/.claude/skills[/dim]',
      '  [dim]Loaded plugins from ~/.claude/plugins[/dim]',
      '[cyan]╭─Welcome─────────────────────────────────────────────────────────╮[/cyan]',
      '[cyan]│[/cyan] [bold]jdai[/bold] — Semantic Kernel TUI Agent                                [cyan]│[/cyan]',
      '[cyan]│[/cyan] Provider: [purple]Ollama[/purple] | Model: [pink]qwen3:30b-instruct[/pink] | Total models: 65 [cyan]│[/cyan]',
      '[cyan]│[/cyan] Type [yellow]/help[/yellow] for commands, [yellow]/quit[/yellow] to exit.                         [cyan]│[/cyan]',
      '[cyan]╰─────────────────────────────────────────────────────────────────╯[/cyan]',
      '',
      '[purple]>[/purple] ',
    ],
  },
  {
    name: 'demo-commands-help',
    title: 'jdai — /help',
    lines: [
      '[purple]>[/purple] [bold]/help[/bold]',
      '',
      '[bold]Available commands:[/bold]',
      '',
      '  [yellow]/autorun[/yellow]          Toggle auto-approve for tools',
      '  [yellow]/checkpoint[/yellow]       List, restore, or clear checkpoints',
      '  [yellow]/clear[/yellow]            Clear chat history',
      '  [yellow]/compact[/yellow]          Force context compaction',
      '  [yellow]/cost[/yellow]             Show token usage',
      '  [yellow]/exit[/yellow]             Exit jdai',
      '  [yellow]/export[/yellow]           Export current session to JSON',
      '  [yellow]/help[/yellow]             Show available commands',
      '  [yellow]/instructions[/yellow]     View loaded project instructions',
      '  [yellow]/model[/yellow]            Switch the active AI model',
      '  [yellow]/models[/yellow]           List all available models',
      '  [yellow]/new[/yellow]              Start a new conversation',
      '  [yellow]/provider[/yellow]         Switch the active provider',
      '  [yellow]/providers[/yellow]        Show detected providers',
      '  [yellow]/quit[/yellow]             Exit jdai',
      '  [yellow]/resume[/yellow]           Resume a saved session',
      '  [yellow]/save[/yellow]             Save current session',
      '  [yellow]/sessions[/yellow]         List saved sessions',
      '  [yellow]/team[/yellow]             Manage agent teams',
      '  [yellow]/update[/yellow]           Check for and install updates',
    ],
  },
  {
    name: 'demo-chat',
    title: 'jdai — Chat',
    lines: [
      '[purple]>[/purple] [bold]What is the Fibonacci sequence? Give a brief explanation and C# code.[/bold]',
      '',
      'The [bold]Fibonacci sequence[/bold] is a series where each number is the sum of the',
      'two preceding ones, starting from 0 and 1:',
      '',
      '  0, 1, 1, 2, 3, 5, 8, 13, 21, 34, ...',
      '',
      'Here\'s a concise C# implementation:',
      '',
      '[dim]```csharp[/dim]',
      '[pink]static[/pink] IEnumerable<[cyan]long[/cyan]> [green]Fibonacci[/green]([cyan]int[/cyan] count)',
      '{',
      '    [cyan]long[/cyan] a = [purple]0[/purple], b = [purple]1[/purple];',
      '    [pink]for[/pink] ([cyan]int[/cyan] i = [purple]0[/purple]; i < count; i++)',
      '    {',
      '        [pink]yield return[/pink] a;',
      '        (a, b) = (b, a + b);',
      '    }',
      '}',
      '',
      '[dim]// Usage:[/dim]',
      '[pink]foreach[/pink] ([cyan]var[/cyan] n [pink]in[/pink] [green]Fibonacci[/green]([purple]10[/purple]))',
      '    Console.[green]Write[/green]([yellow]$"{n} "[/yellow]);',
      '[dim]// Output: 0 1 1 2 3 5 8 13 21 34[/dim]',
      '[dim]```[/dim]',
      '',
      '[dim]Cost: ~420 tokens | 1.2s[/dim]',
    ],
  },
  {
    name: 'demo-tools',
    title: 'jdai — Tool Execution',
    lines: [
      '[purple]>[/purple] [bold]Read the first 5 lines of Program.cs and search for TODO comments[/bold]',
      '',
      '[dim]⚙ Tool: read_file("src/JD.AI/Program.cs", lines: 1-5)[/dim]',
      '[green]✓[/green] [dim]Read 5 lines from src/JD.AI/Program.cs[/dim]',
      '',
      '[dim]```csharp[/dim]',
      '[pink]using[/pink] JD.AI;',
      '[pink]using[/pink] JD.AI.Agent;',
      '[pink]using[/pink] JD.AI.Agent.Checkpointing;',
      '[pink]using[/pink] JD.AI.Agent.Orchestration;',
      '[pink]using[/pink] JD.AI.Commands;',
      '[dim]```[/dim]',
      '',
      '[dim]⚙ Tool: grep(pattern: "TODO", path: ".", glob: "*.cs")[/dim]',
      '[green]✓[/green] [dim]Found 3 matches across 2 files[/dim]',
      '',
      'Found [bold]3 TODO comments[/bold]:',
      '',
      '  [cyan]src/JD.AI/Tools/WebTools.cs:42[/cyan]',
      '    [yellow]// TODO: Add retry logic for transient failures[/yellow]',
      '  [cyan]src/JD.AI/Agent/AgentLoop.cs:187[/cyan]',
      '    [yellow]// TODO: Implement streaming token count[/yellow]',
      '  [cyan]src/JD.AI/Rendering/ChatRenderer.cs:95[/cyan]',
      '    [yellow]// TODO: Support syntax highlighting themes[/yellow]',
      '',
      '[dim]Cost: ~850 tokens | 2.1s[/dim]',
    ],
  },
  {
    name: 'demo-providers',
    title: 'jdai — /providers',
    lines: [
      '[purple]>[/purple] [bold]/providers[/bold]',
      '',
      '[bold]Detected providers:[/bold]',
      '',
      '  [green]✅[/green] [bold]Claude Code[/bold]',
      '     Status: Authenticated',
      '     Models: Claude Opus 4.6, Claude Sonnet 4.6, Claude Haiku 4.5',
      '',
      '  [green]✅[/green] [bold]GitHub Copilot[/bold]',
      '     Status: Authenticated — 3 model(s)',
      '     Models: Claude Sonnet 4.6, GPT-4o, Claude Opus 4.6',
      '',
      '  [green]✅[/green] [bold]Ollama[/bold]',
      '     Status: 59 model(s) available',
      '     Models: qwen3:30b-instruct, qwen3-coder:30b, mistral-nemo:12b, ...',
      '',
      '  Current: [purple]Ollama[/purple] → [pink]qwen3:30b-instruct[/pink]',
    ],
  },
  {
    name: 'demo-subagents',
    title: 'jdai — Subagent Execution',
    lines: [
      '[purple]>[/purple] [bold]Explore this codebase and summarize the architecture[/bold]',
      '',
      '[dim]⚙ Spawning subagent: [b]explore[/b][/dim]',
      '[cyan]┌──────────────────────────────────────────────────────────┐[/cyan]',
      '[cyan]│[/cyan] [bold]Subagent: explore[/bold]                                        [cyan]│[/cyan]',
      '[cyan]│[/cyan] Tools: read_file, grep, glob, git_log                    [cyan]│[/cyan]',
      '[cyan]│[/cyan] Status: [green]Running[/green]                                          [cyan]│[/cyan]',
      '[cyan]└──────────────────────────────────────────────────────────┘[/cyan]',
      '',
      '[dim]  → Scanning project structure...[/dim]',
      '[dim]  → Reading src/JD.AI/Program.cs...[/dim]',
      '[dim]  → Analyzing 24 source files...[/dim]',
      '[dim]  → Mapping dependency graph...[/dim]',
      '[green]✓[/green] Subagent completed in 4.2s',
      '',
      '[bold]Architecture Summary:[/bold]',
      '',
      'JD.AI follows a [bold]modular layered architecture[/bold]:',
      '',
      '  [cyan]Providers/[/cyan]    Multi-provider AI abstraction (Claude, Copilot, Ollama)',
      '  [cyan]Agent/[/cyan]        Core agent loop, sessions, checkpointing, orchestration',
      '  [cyan]Tools/[/cyan]        SK kernel functions (file, search, shell, git, web, memory)',
      '  [cyan]Commands/[/cyan]     Slash command routing (/help, /model, /save, etc.)',
      '  [cyan]Rendering/[/cyan]    TUI rendering, markdown, interactive input, Spectre.Console',
    ],
  },
  {
    name: 'demo-orchestration',
    title: 'jdai — Team Orchestration',
    lines: [
      '[purple]>[/purple] [bold]/team run code-review --strategy fanout[/bold]',
      '',
      '[bold]Team: code-review[/bold] | Strategy: [cyan]Fan-Out[/cyan] | Agents: 3',
      '',
      '[cyan]┌─ Agent Progress ─────────────────────────────────────────┐[/cyan]',
      '[cyan]│[/cyan]  [green]■[/green] [bold]security-reviewer[/bold]    [green]████████████████████[/green] Done (3.1s) [cyan]│[/cyan]',
      '[cyan]│[/cyan]  [yellow]■[/yellow] [bold]style-checker[/bold]        [yellow]████████████░░░░░░░░[/yellow] Running...  [cyan]│[/cyan]',
      '[cyan]│[/cyan]  [yellow]■[/yellow] [bold]logic-analyzer[/bold]       [yellow]██████████░░░░░░░░░░[/yellow] Running...  [cyan]│[/cyan]',
      '[cyan]└──────────────────────────────────────────────────────────┘[/cyan]',
      '',
      '[dim]Synthesizer will merge results when all agents complete.[/dim]',
      '',
      '[bold]security-reviewer[/bold] results:',
      '  [green]✓[/green] No hardcoded secrets detected',
      '  [green]✓[/green] Input validation present on all endpoints',
      '  [yellow]⚠[/yellow] Consider adding rate limiting to WebTools.FetchAsync',
    ],
  },
  {
    name: 'demo-sessions',
    title: 'jdai — Session Management',
    lines: [
      '[purple]>[/purple] [bold]/sessions[/bold]',
      '',
      '[bold]Saved sessions:[/bold]',
      '',
      '  [cyan]1.[/cyan] [bold]refactor-auth-module[/bold]        [dim]2 hours ago[/dim]     12 turns',
      '  [cyan]2.[/cyan] [bold]fix-streaming-bug[/bold]           [dim]yesterday[/dim]       8 turns',
      '  [cyan]3.[/cyan] [bold]add-provider-tests[/bold]          [dim]2 days ago[/dim]      23 turns',
      '  [cyan]4.[/cyan] [bold]documentation-update[/bold]        [dim]3 days ago[/dim]      5 turns',
      '',
      '[purple]>[/purple] [bold]/save my-review-session[/bold]',
      '',
      '[green]✓[/green] Session saved as [bold]my-review-session[/bold] (15 turns, 3 tool calls)',
      '',
      '[purple]>[/purple] [bold]/resume refactor-auth-module[/bold]',
      '',
      '[green]✓[/green] Resumed session [bold]refactor-auth-module[/bold] (12 turns)',
      '[dim]Last message: "Now let\'s add the JWT refresh token rotation..."[/dim]',
    ],
  },
  {
    name: 'demo-cli',
    title: 'jdai — CLI Usage',
    lines: [
      '[dim]$[/dim] jdai [yellow]--help[/yellow]',
      '',
      '[bold]jdai[/bold] — Semantic Kernel TUI Agent',
      '',
      '[bold]Usage:[/bold] jdai [options]',
      '',
      '[bold]Options:[/bold]',
      '  [yellow]--new[/yellow]                         Start a new session',
      '  [yellow]--resume[/yellow] [cyan]<session-id>[/cyan]        Resume a saved session',
      '  [yellow]--model[/yellow] [cyan]<name>[/cyan]               Select model by name (skips picker)',
      '  [yellow]--provider[/yellow] [cyan]<name>[/cyan]            Filter to a specific provider',
      '  [yellow]--dangerously-skip-permissions[/yellow] Auto-approve all tool calls',
      '  [yellow]--force-update-check[/yellow]          Force update check on startup',
      '',
      '[bold]Examples:[/bold]',
      '  [dim]$[/dim] jdai',
      '  [dim]$[/dim] jdai --new --model [cyan]"sonnet"[/cyan]',
      '  [dim]$[/dim] jdai --provider [cyan]"ollama"[/cyan] --model [cyan]"qwen3:30b"[/cyan]',
      '  [dim]$[/dim] jdai --resume [cyan]"my-session"[/cyan]',
    ],
  },
];

// ── Generate ────────────────────────────────────────────────────

async function main() {
  console.log('Launching browser...');
  const browser = await chromium.launch();
  const context = await browser.newContext({ deviceScaleFactor: 2 });

  for (const shot of screenshots) {
    const page = await context.newPage();
    const html = buildHtml(shot.title, shot.lines, { width: shot.width || 900 });
    await page.setContent(html, { waitUntil: 'networkidle' });

    // Wait for font to load
    await page.waitForTimeout(1000);

    // Size viewport to content
    const box = await page.locator('.terminal').boundingBox();
    if (box) {
      await page.setViewportSize({
        width: Math.ceil(box.x * 2 + box.width + 48),
        height: Math.ceil(box.y * 2 + box.height + 48),
      });
    }

    const outPath = join(imagesDir, `${shot.name}.png`);
    await page.locator('.terminal').screenshot({ path: outPath });
    console.log(`  ✅ ${shot.name}.png`);
    await page.close();
  }

  await browser.close();
  console.log(`\nDone! ${screenshots.length} screenshots in docs/images/`);
}

main().catch(e => { console.error(e); process.exit(1); });
