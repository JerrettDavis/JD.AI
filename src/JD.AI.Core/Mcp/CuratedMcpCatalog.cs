namespace JD.AI.Core.Mcp;

/// <summary>
/// Curated registry of well-known MCP servers that JD.AI recommends during onboarding.
/// Each entry provides enough metadata to install the server and prompt for any required
/// credentials or arguments without further user research.
/// </summary>
public static class CuratedMcpCatalog
{
    /// <summary>Returns the full curated catalog, ordered by category then display name.</summary>
    public static IReadOnlyList<CuratedMcpEntry> All { get; } = Build();

    private static IReadOnlyList<CuratedMcpEntry> Build() =>
    [
        // ── Source Control ────────────────────────────────────────────────────
        new(
            Id: "github",
            DisplayName: "GitHub MCP",
            Category: "Source Control",
            Description: "Manage repos, issues, PRs, Actions, and Copilot from any agent.",
            Transport: CuratedMcpTransport.Http,
            Url: "https://api.githubcopilot.com/mcp/",
            RequiredEnvVars: null, // OAuth handled by the host; PAT optional
            DocsUrl: "https://github.com/github/github-mcp-server",
            InstallNote: "Uses GitHub Copilot OAuth. A PAT is only needed when OAuth is unavailable."),

        new(
            Id: "git",
            DisplayName: "Git (local)",
            Category: "Source Control",
            Description: "Read, search, and manipulate local Git repositories.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "uvx",
            DefaultArgs: ["mcp-server-git"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/git",
            InstallNote: "Requires Python/uv. Install with: pip install uv"),

        new(
            Id: "azure-devops",
            DisplayName: "Azure DevOps MCP",
            Category: "Source Control",
            Description: "List projects, repos, pipelines, work items, test plans, and wikis.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@azure-devops/mcp", "{organization}"],
            PromptArgs:
            [
                new("organization", "Azure DevOps organization name", Example: "contoso"),
            ],
            RequiredEnvVars:
            [
                new("AZURE_DEVOPS_PAT", "Azure DevOps Personal Access Token"),
            ],
            DocsUrl: "https://github.com/microsoft/azure-devops-mcp",
            InstallNote: "Requires Node.js 20+."),

        // ── Developer Tools ───────────────────────────────────────────────────
        new(
            Id: "desktop-commander",
            DisplayName: "Desktop Commander",
            Category: "Developer Tools",
            Description: "Execute terminal commands, manage processes, and edit files with diff/patch support.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@wonderwhy-er/desktop-commander"],
            DocsUrl: "https://github.com/wonderwhy-er/DesktopCommanderMCP",
            InstallNote: "Requires Node.js."),

        new(
            Id: "fetch",
            DisplayName: "Fetch (web)",
            Category: "Developer Tools",
            Description: "Fetch web pages and convert them to Markdown for efficient LLM consumption.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "uvx",
            DefaultArgs: ["mcp-server-fetch"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/fetch",
            InstallNote: "Requires Python/uv."),

        new(
            Id: "filesystem",
            DisplayName: "Filesystem",
            Category: "Developer Tools",
            Description: "Secure read/write file operations with configurable access controls.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-filesystem"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem",
            InstallNote: "Requires Node.js."),

        new(
            Id: "memory",
            DisplayName: "Memory (knowledge graph)",
            Category: "Developer Tools",
            Description: "Persistent in-session memory using a local knowledge graph.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-memory"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/memory",
            InstallNote: "Requires Node.js."),

        new(
            Id: "puppeteer",
            DisplayName: "Puppeteer (browser)",
            Category: "Developer Tools",
            Description: "Headless browser automation and web scraping.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-puppeteer"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers-archived/tree/main/src/puppeteer",
            InstallNote: "Requires Node.js and Chromium."),

        new(
            Id: "sequential-thinking",
            DisplayName: "Sequential Thinking",
            Category: "Developer Tools",
            Description: "Dynamic step-by-step reasoning and reflective problem-solving.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-sequential-thinking"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/sequentialthinking",
            InstallNote: "Requires Node.js."),

        new(
            Id: "time",
            DisplayName: "Time & Timezone",
            Category: "Developer Tools",
            Description: "Current time, timezone lookup, and timezone conversion.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "uvx",
            DefaultArgs: ["mcp-server-time"],
            DocsUrl: "https://github.com/modelcontextprotocol/servers/tree/main/src/time",
            InstallNote: "Requires Python/uv."),

        // ── Windows Desktop ───────────────────────────────────────────────────
        new(
            Id: "windows-mcp",
            DisplayName: "Windows Desktop Control",
            Category: "Windows Desktop",
            Description: "Capture screenshots, move mouse, type, launch apps, and control the Windows UI.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "uvx",
            DefaultArgs: ["windows-mcp"],
            DocsUrl: "https://github.com/CursorTouch/Windows-MCP",
            InstallNote: "Windows only. Requires Python/uv."),

        // ── Productivity ──────────────────────────────────────────────────────
        new(
            Id: "notion",
            DisplayName: "Notion",
            Category: "Productivity",
            Description: "Read and write Notion pages, databases, and blocks.",
            Transport: CuratedMcpTransport.Http,
            Url: "https://mcp.notion.com/mcp",
            RequiredEnvVars:
            [
                new("NOTION_TOKEN", "Notion integration token (starts with 'secret_')"),
            ],
            DocsUrl: "https://developers.notion.com/docs/mcp",
            InstallNote: "Requires a Notion integration with appropriate permissions."),

        new(
            Id: "slack",
            DisplayName: "Slack",
            Category: "Productivity",
            Description: "Send messages, read channels, and manage Slack workspaces.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@zencoderai/slack-mcp-server"],
            RequiredEnvVars:
            [
                new("SLACK_BOT_TOKEN", "Slack bot token (starts with 'xoxb-')"),
                new("SLACK_TEAM_ID", "Slack workspace/team ID", IsSecret: false),
            ],
            DocsUrl: "https://github.com/zencoderai/slack-mcp-server",
            InstallNote: "Requires Node.js and a Slack app with bot token."),

        // ── Search & AI ───────────────────────────────────────────────────────
        new(
            Id: "brave-search",
            DisplayName: "Brave Search",
            Category: "Search & AI",
            Description: "Real-time web and local search powered by the Brave Search API.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@brave/brave-search-mcp-server"],
            RequiredEnvVars:
            [
                new("BRAVE_API_KEY", "Brave Search API key"),
            ],
            DocsUrl: "https://github.com/brave/brave-search-mcp-server",
            InstallNote: "Free tier available at search.brave.com/api."),

        // ── Databases ─────────────────────────────────────────────────────────
        new(
            Id: "postgres",
            DisplayName: "PostgreSQL",
            Category: "Databases",
            Description: "Read-only query access to a PostgreSQL database with schema inspection.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@modelcontextprotocol/server-postgres"],
            RequiredEnvVars:
            [
                new("DATABASE_URL", "PostgreSQL connection string (e.g. postgresql://user:pass@host/db)"),
            ],
            DocsUrl: "https://github.com/modelcontextprotocol/servers-archived/tree/main/src/postgres",
            InstallNote: "Requires Node.js."),

        new(
            Id: "sqlite",
            DisplayName: "SQLite",
            Category: "Databases",
            Description: "Query and inspect a local SQLite database file.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "uvx",
            DefaultArgs: ["mcp-server-sqlite", "--db-path", "{db_path}"],
            PromptArgs:
            [
                new("db_path", "Path to the SQLite database file", Example: "~/mydb.sqlite"),
            ],
            DocsUrl: "https://github.com/modelcontextprotocol/servers-archived/tree/main/src/sqlite",
            InstallNote: "Requires Python/uv."),

        // ── Cloud & Infra ─────────────────────────────────────────────────────
        new(
            Id: "azure",
            DisplayName: "Azure Services",
            Category: "Cloud & Infra",
            Description: "Manage Azure resources, subscriptions, and services.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@azure/mcp"],
            DocsUrl: "https://github.com/Azure/azure-mcp",
            InstallNote: "Requires Node.js and Azure CLI (az login)."),

        // ── Communication ────────────────────────────────────────────────────
        new(
            Id: "discord",
            DisplayName: "Discord MCP",
            Category: "Communication",
            Description: "Read messages, manage channels, create threads, send embeds, manage reactions, and interact with Discord servers via the bot's authenticated session.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@punkpeye/discord-mcp"],
            RequiredEnvVars:
            [
                new("DISCORD_TOKEN", "Discord Bot Token", IsSecret: true),
            ],
            DocsUrl: "https://github.com/punkpeye/discord-mcp",
            InstallNote: "Requires Node.js. Uses your Discord bot token for authentication. Gives the agent full access to read history, manage channels, create threads, and react to messages."),

        new(
            Id: "slack",
            DisplayName: "Slack MCP",
            Category: "Communication",
            Description: "Read and send Slack messages, manage channels, and search workspace history.",
            Transport: CuratedMcpTransport.Stdio,
            Command: "npx",
            DefaultArgs: ["-y", "@anthropic/slack-mcp"],
            RequiredEnvVars:
            [
                new("SLACK_BOT_TOKEN", "Slack Bot Token (xoxb-...)", IsSecret: true),
            ],
            DocsUrl: "https://github.com/anthropics/slack-mcp",
            InstallNote: "Requires Node.js. Needs a Slack app with bot token scope."),
    ];
}
