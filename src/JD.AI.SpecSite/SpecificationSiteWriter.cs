using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;

namespace JD.AI.SpecSite;

public static class SpecificationSiteWriter
{
    public static void Write(SpecSiteOptions options, IReadOnlyList<SpecificationCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalogs);

        PrepareOutputDirectory(options.RepoRoot, options.OutputRoot);
        WriteSharedAssets(options.OutputRoot);

        var documentPaths = BuildDocumentPathMap(catalogs);
        WriteHomePage(options, catalogs, documentPaths);

        foreach (var catalog in catalogs)
        {
            WriteTypeIndexPage(options, catalogs, documentPaths, catalog);

            foreach (var document in catalog.Documents)
                WriteDocumentPage(options, catalogs, documentPaths, catalog, document);
        }
    }

    private static void PrepareOutputDirectory(string repoRoot, string outputRoot)
    {
        var normalizedRepoRoot = Path.GetFullPath(repoRoot);
        var normalizedOutputRoot = Path.GetFullPath(outputRoot);

        if (string.Equals(normalizedRepoRoot, normalizedOutputRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Output directory cannot be the repository root.");

        if (Directory.Exists(normalizedOutputRoot))
            Directory.Delete(normalizedOutputRoot, recursive: true);
        Directory.CreateDirectory(normalizedOutputRoot);
    }

    private static ReadOnlyDictionary<(string TypeName, string Id), string> BuildDocumentPathMap(
        IReadOnlyList<SpecificationCatalog> catalogs)
    {
        var map = new Dictionary<(string TypeName, string Id), string>();
        foreach (var catalog in catalogs)
        {
            foreach (var document in catalog.Documents)
            {
                map[(catalog.TypeName, document.Id)] =
                    SpecSitePathHelper.GetDocumentRelativePath(catalog.TypeName, document.Id);
            }
        }

        return new ReadOnlyDictionary<(string TypeName, string Id), string>(map);
    }

    private static void WriteHomePage(
        SpecSiteOptions options,
        IReadOnlyList<SpecificationCatalog> catalogs,
        ReadOnlyDictionary<(string TypeName, string Id), string> documentPaths)
    {
        var outputPath = Path.Combine(options.OutputRoot, "index.html");
        var totalSpecifications = catalogs.Sum(catalog => catalog.Documents.Count);
        var cards = new StringBuilder();
        foreach (var catalog in catalogs)
        {
            var typeIndexPath = SpecSitePathHelper.GetTypeIndexRelativePath(catalog.TypeName);
            cards.AppendLine($"""
                <a class="type-card" href="{HtmlEncode(typeIndexPath)}">
                  <div class="type-card-header">
                    <h2>{HtmlEncode(ToDisplayName(catalog.TypeName))}</h2>
                    <span class="pill">{catalog.Documents.Count.ToString(CultureInfo.InvariantCulture)}</span>
                  </div>
                  <p class="type-card-meta">{HtmlEncode(catalog.IndexKind)}</p>
                </a>
                """);
        }

        var page = BuildPage(
            options,
            catalogs,
            documentPaths,
            currentPageAbsolutePath: outputPath,
            pageTitle: options.SiteTitle,
            heroTitle: options.SiteTitle,
            heroSubtitle: "Repository-native UPSS specifications rendered into a navigable review portal.",
            bodyHtml: $"""
                <section class="overview-panel">
                  <div class="metric-tile">
                    <span class="metric-value">{catalogs.Count.ToString(CultureInfo.InvariantCulture)}</span>
                    <span class="metric-label">Spec Types</span>
                  </div>
                  <div class="metric-tile">
                    <span class="metric-value">{totalSpecifications.ToString(CultureInfo.InvariantCulture)}</span>
                    <span class="metric-label">Specifications</span>
                  </div>
                  <div class="metric-tile">
                    <span class="metric-value">UPSS</span>
                    <span class="metric-label">Unified Spec Graph</span>
                  </div>
                </section>
                <section class="cards-grid" aria-label="Specification type catalog">
                  {cards}
                </section>
                """);

        File.WriteAllText(outputPath, page);
    }

    private static void WriteTypeIndexPage(
        SpecSiteOptions options,
        IReadOnlyList<SpecificationCatalog> catalogs,
        ReadOnlyDictionary<(string TypeName, string Id), string> documentPaths,
        SpecificationCatalog catalog)
    {
        var relativePath = SpecSitePathHelper.GetTypeIndexRelativePath(catalog.TypeName);
        var outputPath = Path.Combine(
            options.OutputRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var links = new StringBuilder();
        foreach (var document in catalog.Documents)
        {
            var docRelativePath = documentPaths[(catalog.TypeName, document.Id)];
            var href = GetRelativeHref(outputPath, Path.Combine(
                options.OutputRoot,
                docRelativePath.Replace('/', Path.DirectorySeparatorChar)));

            links.AppendLine($"""
                <a class="spec-link" href="{HtmlEncode(href)}">
                  <div class="spec-link-header">
                    <h3>{HtmlEncode(document.Title)}</h3>
                    <span class="status {HtmlEncode(StatusClass(document.Status))}">
                      {HtmlEncode(document.Status)}
                    </span>
                  </div>
                  <p class="spec-link-meta">{HtmlEncode(document.Id)}</p>
                </a>
                """);
        }

        var page = BuildPage(
            options,
            catalogs,
            documentPaths,
            currentPageAbsolutePath: outputPath,
            pageTitle: $"{ToDisplayName(catalog.TypeName)} Specs",
            heroTitle: $"{ToDisplayName(catalog.TypeName)} Specifications",
            heroSubtitle: $"{catalog.Documents.Count.ToString(CultureInfo.InvariantCulture)} specifications",
            bodyHtml: $"""
                <section class="spec-list">
                {links}
                </section>
                """);

        File.WriteAllText(outputPath, page);
    }

    private static void WriteDocumentPage(
        SpecSiteOptions options,
        IReadOnlyList<SpecificationCatalog> catalogs,
        ReadOnlyDictionary<(string TypeName, string Id), string> documentPaths,
        SpecificationCatalog catalog,
        SpecificationDocument document)
    {
        var relativePath = documentPaths[(catalog.TypeName, document.Id)];
        var outputPath = Path.Combine(
            options.OutputRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var body = new StringBuilder();
        body.AppendLine($"""
            <section class="spec-summary">
              <dl>
                <dt>ID</dt>
                <dd><code>{HtmlEncode(document.Id)}</code></dd>
                <dt>Status</dt>
                <dd>
                  <span class="status {HtmlEncode(StatusClass(document.Status))}">
                    {HtmlEncode(document.Status)}
                  </span>
                </dd>
                <dt>Source</dt>
                <dd><code>{HtmlEncode(document.SourceRelativePath)}</code></dd>
              </dl>
            </section>
            """);

        foreach (var property in document.RootNode.Properties)
        {
            body.AppendLine($"""
                <section class="spec-section">
                  <h2>{HtmlEncode(ToDisplayName(property.Key))}</h2>
                  {RenderNode(property.Value)}
                </section>
                """);
        }

        body.AppendLine($"""
            <section class="spec-section">
              <h2>Raw YAML</h2>
              <pre class="yaml-source"><code>{HtmlEncode(document.SourceYaml)}</code></pre>
            </section>
            """);

        var page = BuildPage(
            options,
            catalogs,
            documentPaths,
            currentPageAbsolutePath: outputPath,
            pageTitle: document.Title,
            heroTitle: document.Title,
            heroSubtitle: document.Id,
            bodyHtml: body.ToString());

        File.WriteAllText(outputPath, page);
    }

    private static string BuildPage(
        SpecSiteOptions options,
        IReadOnlyList<SpecificationCatalog> catalogs,
        ReadOnlyDictionary<(string TypeName, string Id), string> documentPaths,
        string currentPageAbsolutePath,
        string pageTitle,
        string heroTitle,
        string heroSubtitle,
        string bodyHtml)
    {
        var cssHref = GetRelativeHref(
            currentPageAbsolutePath,
            Path.Combine(options.OutputRoot, "site.css"));
        var jsHref = GetRelativeHref(
            currentPageAbsolutePath,
            Path.Combine(options.OutputRoot, "site.js"));

        var sidebar = BuildSidebar(options, catalogs, documentPaths, currentPageAbsolutePath);
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta name="color-scheme" content="light dark" />
              <title>{HtmlEncode(pageTitle)} | {HtmlEncode(options.SiteTitle)}</title>
              <link rel="stylesheet" href="{HtmlEncode(cssHref)}" />
            </head>
            <body>
              <a class="skip-link" href="#main-content">Skip to content</a>
              <div class="shell">
                <aside class="sidebar">
                  <div class="sidebar-header">
                    <a class="brand-link" href="{HtmlEncode(GetRelativeHref(currentPageAbsolutePath, Path.Combine(options.OutputRoot, "index.html")))}">
                      {HtmlEncode(options.SiteTitle)}
                    </a>
                    <div class="sidebar-controls">
                      <input id="nav-filter" type="search" placeholder="Filter specs..." aria-label="Filter specs" />
                      <button id="theme-toggle" type="button" aria-label="Toggle theme">Theme: Auto</button>
                    </div>
                  </div>
                  <nav id="spec-nav">
                    {sidebar}
                  </nav>
                </aside>
                <main id="main-content" class="content">
                  <header class="hero">
                    <h1>{HtmlEncode(heroTitle)}</h1>
                    <p>{HtmlEncode(heroSubtitle)}</p>
                  </header>
                  {bodyHtml}
                </main>
              </div>
              <script src="{HtmlEncode(jsHref)}"></script>
            </body>
            </html>
            """;
    }

    private static string BuildSidebar(
        SpecSiteOptions options,
        IReadOnlyList<SpecificationCatalog> catalogs,
        ReadOnlyDictionary<(string TypeName, string Id), string> documentPaths,
        string currentPageAbsolutePath)
    {
        var builder = new StringBuilder();
        var homePath = Path.Combine(options.OutputRoot, "index.html");
        var homeHref = GetRelativeHref(currentPageAbsolutePath, homePath);

        builder.AppendLine($"""
            <a class="nav-home {HtmlEncode(IsCurrent(currentPageAbsolutePath, homePath) ? "active" : string.Empty)}"
               href="{HtmlEncode(homeHref)}">
              Overview
            </a>
            """);

        foreach (var catalog in catalogs)
        {
            var typeIndexRelative = SpecSitePathHelper.GetTypeIndexRelativePath(catalog.TypeName);
            var typeIndexAbsolute = Path.Combine(
                options.OutputRoot,
                typeIndexRelative.Replace('/', Path.DirectorySeparatorChar));
            var typeHref = GetRelativeHref(currentPageAbsolutePath, typeIndexAbsolute);
            var typeIsActive = IsCurrent(currentPageAbsolutePath, typeIndexAbsolute);
            var displayTypeName = ToDisplayName(catalog.TypeName);

            builder.AppendLine($"""
                <section class="nav-group" data-group-label="{HtmlEncode(displayTypeName)}">
                  <a class="nav-group-title {HtmlEncode(typeIsActive ? "active" : string.Empty)}"
                     href="{HtmlEncode(typeHref)}">
                    {HtmlEncode(displayTypeName)}
                  </a>
                  <ul>
                """);

            foreach (var document in catalog.Documents)
            {
                var relative = documentPaths[(catalog.TypeName, document.Id)];
                var absolute = Path.Combine(
                    options.OutputRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                var href = GetRelativeHref(currentPageAbsolutePath, absolute);
                var isActive = IsCurrent(currentPageAbsolutePath, absolute);

                builder.AppendLine($"""
                    <li>
                      <a class="{HtmlEncode(isActive ? "active" : string.Empty)}"
                         href="{HtmlEncode(href)}"
                         data-label="{HtmlEncode(document.Id)} {HtmlEncode(document.Title)}">
                        {HtmlEncode(document.Title)}
                      </a>
                    </li>
                    """);
            }

            builder.AppendLine("""
                  </ul>
                </section>
                """);
        }

        return builder.ToString();
    }

    private static string RenderNode(YamlNode node)
    {
        return node switch
        {
            YamlMapNode map => RenderMap(map),
            YamlSequenceNode sequence => RenderSequence(sequence),
            YamlScalarNode scalar => RenderScalar(scalar),
            _ => string.Empty,
        };
    }

    private static string RenderMap(YamlMapNode map)
    {
        if (map.Properties.Count == 0)
            return "<p class=\"empty\">(empty object)</p>";

        var builder = new StringBuilder();
        builder.AppendLine("<dl class=\"yaml-map\">");
        foreach (var property in map.Properties)
        {
            builder.AppendLine($"<dt>{HtmlEncode(property.Key)}</dt>");
            builder.AppendLine($"<dd>{RenderNode(property.Value)}</dd>");
        }

        builder.AppendLine("</dl>");
        return builder.ToString();
    }

    private static string RenderSequence(YamlSequenceNode sequence)
    {
        if (sequence.Items.Count == 0)
            return "<p class=\"empty\">(empty list)</p>";

        var builder = new StringBuilder();
        builder.AppendLine("<ol class=\"yaml-sequence\">");
        foreach (var item in sequence.Items)
            builder.AppendLine($"<li>{RenderNode(item)}</li>");
        builder.AppendLine("</ol>");
        return builder.ToString();
    }

    private static string RenderScalar(YamlScalarNode scalar)
    {
        if (scalar.Value is null)
            return "<code class=\"scalar-null\">null</code>";

        if (scalar.Value is bool boolean)
            return $"<code>{boolean.ToString().ToLowerInvariant()}</code>";

        if (scalar.Value is string text)
        {
            if (text.Contains('\n', StringComparison.Ordinal))
                return $"<pre class=\"scalar-block\">{HtmlEncode(text)}</pre>";
            return $"<span>{HtmlEncode(text)}</span>";
        }

        var value = Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        return $"<code>{HtmlEncode(value)}</code>";
    }

    private static void WriteSharedAssets(string outputRoot)
    {
        File.WriteAllText(Path.Combine(outputRoot, "site.css"), Stylesheet);
        File.WriteAllText(Path.Combine(outputRoot, "site.js"), Script);
    }

    private static string GetRelativeHref(string fromPageAbsolutePath, string toPageAbsolutePath)
    {
        var sourceDirectory = Path.GetDirectoryName(fromPageAbsolutePath)
            ?? throw new InvalidOperationException(
                $"Unable to resolve source directory for '{fromPageAbsolutePath}'.");
        var relative = Path.GetRelativePath(sourceDirectory, toPageAbsolutePath);
        return SpecSitePathHelper.NormalizeWebPath(relative);
    }

    private static string ToDisplayName(string value)
    {
        var normalized = value
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();

        return normalized.ToLowerInvariant() switch
        {
            "adrs" => "ADRs",
            "usecases" => "Use Cases",
            _ => string.Join(
                " ",
                normalized
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant())),
        };
    }

    private static bool IsCurrent(string currentPath, string candidatePath) =>
        string.Equals(
            Path.GetFullPath(currentPath),
            Path.GetFullPath(candidatePath),
            StringComparison.OrdinalIgnoreCase);

    private static string StatusClass(string status) =>
        status.ToLowerInvariant() switch
        {
            "active" => "status-active",
            "deprecated" => "status-deprecated",
            "retired" => "status-retired",
            _ => "status-draft",
        };

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);

    private const string Script = """
        (() => {
          const root = document.documentElement;
          const themeToggle = document.getElementById('theme-toggle');
          const storageKey = 'jdai-specsite-theme';
          const cycle = ['system', 'light', 'dark'];
          const media = window.matchMedia('(prefers-color-scheme: dark)');

          const readStored = () => {
            const value = localStorage.getItem(storageKey);
            return cycle.includes(value) ? value : 'system';
          };

          let selectedTheme = readStored();

          const resolvedTheme = () =>
            selectedTheme === 'system'
              ? (media.matches ? 'dark' : 'light')
              : selectedTheme;

          const applyTheme = () => {
            if (selectedTheme === 'system') {
              root.removeAttribute('data-theme');
            } else {
              root.setAttribute('data-theme', selectedTheme);
            }
          };

          const updateToggleLabel = () => {
            if (!themeToggle) {
              return;
            }

            if (selectedTheme === 'system') {
              themeToggle.textContent = `Theme: Auto (${resolvedTheme()})`;
              return;
            }

            themeToggle.textContent = `Theme: ${selectedTheme[0].toUpperCase()}${selectedTheme.slice(1)}`;
          };

          applyTheme();
          updateToggleLabel();

          media.addEventListener('change', () => {
            if (selectedTheme !== 'system') {
              return;
            }

            applyTheme();
            updateToggleLabel();
          });

          if (themeToggle) {
            themeToggle.addEventListener('click', () => {
              const currentIndex = cycle.indexOf(selectedTheme);
              selectedTheme = cycle[(currentIndex + 1) % cycle.length];

              if (selectedTheme === 'system') {
                localStorage.removeItem(storageKey);
              } else {
                localStorage.setItem(storageKey, selectedTheme);
              }

              applyTheme();
              updateToggleLabel();
            });
          }

          const input = document.getElementById('nav-filter');
          const nav = document.getElementById('spec-nav');
          if (!input || !nav) {
            return;
          }

          input.addEventListener('input', () => {
            const term = input.value.trim().toLowerCase();
            const links = nav.querySelectorAll('a[data-label]');
            links.forEach((link) => {
              const label = (link.getAttribute('data-label') || '').toLowerCase();
              const item = link.closest('li');
              if (!item) {
                return;
              }

              item.style.display = term.length === 0 || label.includes(term)
                ? ''
                : 'none';
            });

            const groups = nav.querySelectorAll('.nav-group');
            groups.forEach((group) => {
              const items = Array.from(group.querySelectorAll('li'));
              const hasVisibleItem = items.some((item) => item.style.display !== 'none');
              const groupLabel = (group.getAttribute('data-group-label') || '').toLowerCase();
              const showGroup = term.length === 0 || hasVisibleItem || groupLabel.includes(term);

              group.style.display = showGroup ? '' : 'none';

              if (term.length > 0 && !hasVisibleItem && groupLabel.includes(term)) {
                items.forEach((item) => {
                  item.style.display = '';
                });
              }
            });
          });
        })();
        """;

    private const string Stylesheet = """
        :root {
          color-scheme: light dark;
          --font-display: "Fraunces", "Iowan Old Style", "Palatino Linotype", "Book Antiqua", serif;
          --font-body: "Source Sans Pro", "Trebuchet MS", "Gill Sans", sans-serif;
          --font-mono: "JetBrains Mono", "Cascadia Code", "Consolas", monospace;
          --radius-xl: 20px;
          --radius-lg: 14px;
          --radius-sm: 10px;
          --bg: #f3efe6;
          --bg-alt: #ece6d7;
          --panel: #fffdf8;
          --panel-soft: #f8f4ea;
          --text: #1a1712;
          --muted: #5f5a4f;
          --line: #dbd2c0;
          --brand: #0a5f73;
          --brand-soft: #dbedf2;
          --accent: #b35e17;
          --ok: #1d7a43;
          --warn: #925d00;
          --danger: #9b2323;
          --draft: #656565;
          --shadow: 0 16px 34px rgba(39, 31, 15, 0.08);
          --surface-shadow: 0 1px 0 rgba(255, 255, 255, 0.45) inset;
        }

        :root[data-theme="light"] {
          --bg: #f3efe6;
          --bg-alt: #ece6d7;
          --panel: #fffdf8;
          --panel-soft: #f8f4ea;
          --text: #1a1712;
          --muted: #5f5a4f;
          --line: #dbd2c0;
          --brand: #0a5f73;
          --brand-soft: #dbedf2;
          --accent: #b35e17;
          --ok: #1d7a43;
          --warn: #925d00;
          --danger: #9b2323;
          --draft: #656565;
          --shadow: 0 16px 34px rgba(39, 31, 15, 0.08);
          --surface-shadow: 0 1px 0 rgba(255, 255, 255, 0.45) inset;
        }

        :root[data-theme="dark"] {
          --bg: #101218;
          --bg-alt: #0d0f15;
          --panel: #171c24;
          --panel-soft: #1b2230;
          --text: #eff3ff;
          --muted: #9faac4;
          --line: #2b3346;
          --brand: #8ad8f0;
          --brand-soft: #1b3440;
          --accent: #ffb169;
          --ok: #66d69a;
          --warn: #f2c45b;
          --danger: #ff8f8f;
          --draft: #c1c9da;
          --shadow: 0 20px 40px rgba(0, 0, 0, 0.38);
          --surface-shadow: 0 1px 0 rgba(255, 255, 255, 0.03) inset;
        }

        @media (prefers-color-scheme: dark) {
          :root:not([data-theme]) {
            --bg: #101218;
            --bg-alt: #0d0f15;
            --panel: #171c24;
            --panel-soft: #1b2230;
            --text: #eff3ff;
            --muted: #9faac4;
            --line: #2b3346;
            --brand: #8ad8f0;
            --brand-soft: #1b3440;
            --accent: #ffb169;
            --ok: #66d69a;
            --warn: #f2c45b;
            --danger: #ff8f8f;
            --draft: #c1c9da;
            --shadow: 0 20px 40px rgba(0, 0, 0, 0.38);
            --surface-shadow: 0 1px 0 rgba(255, 255, 255, 0.03) inset;
          }
        }

        * {
          box-sizing: border-box;
        }

        html, body {
          margin: 0;
          padding: 0;
          min-height: 100%;
          color: var(--text);
          background-color: var(--bg);
          background:
            radial-gradient(circle at 0% 0%, color-mix(in srgb, var(--accent) 18%, transparent) 0, transparent 34%),
            radial-gradient(circle at 100% 0%, color-mix(in srgb, var(--brand) 24%, transparent) 0, transparent 38%),
            linear-gradient(110deg, transparent 0 35%, color-mix(in srgb, var(--line) 22%, transparent) 35% 36%, transparent 36% 70%, color-mix(in srgb, var(--line) 18%, transparent) 70% 71%, transparent 71%),
            var(--bg);
          font-family: var(--font-body);
          line-height: 1.45;
        }

        .skip-link {
          position: absolute;
          left: 0.5rem;
          top: -3rem;
          z-index: 999;
          background: var(--panel);
          color: var(--text);
          border: 1px solid var(--line);
          border-radius: var(--radius-sm);
          padding: 0.55rem 0.7rem;
          text-decoration: none;
          box-shadow: var(--shadow);
        }

        .skip-link:focus {
          top: 0.5rem;
        }

        .shell {
          display: grid;
          grid-template-columns: minmax(280px, 340px) 1fr;
          min-height: 100vh;
        }

        .sidebar {
          border-right: 1px solid var(--line);
          background: linear-gradient(180deg, var(--panel) 0%, var(--panel-soft) 100%);
          background: linear-gradient(180deg, color-mix(in srgb, var(--panel) 90%, transparent) 0%, color-mix(in srgb, var(--panel-soft) 94%, transparent) 100%);
          backdrop-filter: blur(8px);
          padding: 1.15rem 0.95rem 1rem;
          position: sticky;
          top: 0;
          height: 100vh;
          overflow-y: auto;
          box-shadow: var(--surface-shadow);
        }

        .sidebar-header {
          margin-bottom: 0.75rem;
        }

        .brand-link {
          font-weight: 800;
          letter-spacing: 0.01em;
          text-decoration: none;
          color: var(--brand);
          display: block;
          margin-bottom: 0.6rem;
          font-family: var(--font-display);
          font-size: 1.12rem;
        }

        .sidebar-controls {
          display: grid;
          grid-template-columns: 1fr auto;
          gap: 0.45rem;
        }

        #nav-filter {
          width: 100%;
          border: 1px solid var(--line);
          border-radius: var(--radius-sm);
          padding: 0.55rem 0.7rem;
          background: var(--panel);
          color: var(--text);
          font-size: 0.92rem;
        }

        #nav-filter::placeholder {
          color: var(--muted);
          color: color-mix(in srgb, var(--muted) 85%, transparent);
        }

        #theme-toggle {
          border: 1px solid var(--line);
          border-radius: var(--radius-sm);
          background: var(--panel);
          color: var(--text);
          padding: 0.5rem 0.62rem;
          font-size: 0.83rem;
          cursor: pointer;
          transition: border-color 120ms ease, transform 120ms ease;
        }

        #theme-toggle:hover {
          border-color: var(--brand);
          transform: translateY(-1px);
        }

        .nav-home,
        .nav-group-title,
        .nav-group a {
          text-decoration: none;
          color: var(--text);
        }

        .nav-home,
        .nav-group-title {
          font-weight: 700;
          display: block;
          padding: 0.47rem 0.57rem;
          border-radius: var(--radius-sm);
          margin-top: 0.42rem;
          transition: background-color 130ms ease, color 130ms ease;
        }

        .nav-group ul {
          margin: 0.25rem 0 0.78rem 0;
          padding-left: 1.05rem;
          list-style: none;
        }

        .nav-group li a {
          display: inline-block;
          padding: 0.2rem 0.35rem;
          border-radius: 7px;
          font-size: 0.9rem;
          color: var(--muted);
        }

        .active {
          background: var(--brand-soft);
          color: var(--brand) !important;
        }

        .content {
          padding: 1.7rem clamp(0.95rem, 2.8vw, 2.35rem);
          animation: reveal 220ms ease-out;
        }

        @keyframes reveal {
          from { opacity: 0; transform: translateY(6px); }
          to { opacity: 1; transform: translateY(0); }
        }

        .hero {
          border: 1px solid var(--line);
          border-radius: var(--radius-xl);
          background: linear-gradient(140deg, var(--panel) 0%, var(--panel-soft) 100%);
          background:
            linear-gradient(140deg, color-mix(in srgb, var(--panel) 86%, var(--bg-alt)) 0%, color-mix(in srgb, var(--brand) 13%, var(--panel)) 62%, color-mix(in srgb, var(--accent) 11%, var(--panel)) 100%);
          padding: 1.25rem 1.35rem;
          margin-bottom: 1rem;
          box-shadow: var(--shadow);
          position: relative;
          overflow: hidden;
        }

        .hero::after {
          content: "";
          position: absolute;
          inset: auto -24% -70% 40%;
          height: 220px;
          background: radial-gradient(circle, rgba(74, 170, 204, 0.2) 0%, transparent 72%);
          background: radial-gradient(circle, color-mix(in srgb, var(--brand) 26%, transparent) 0%, transparent 72%);
          pointer-events: none;
        }

        .hero h1 {
          margin: 0 0 0.35rem;
          font-size: clamp(1.45rem, 2.3vw, 2.15rem);
          color: var(--text);
          font-family: var(--font-display);
          letter-spacing: 0.01em;
        }

        .hero p {
          margin: 0;
          color: var(--muted);
          max-width: 68ch;
        }

        .overview-panel {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
          gap: 0.8rem;
          margin-bottom: 0.9rem;
        }

        .metric-tile {
          border: 1px solid var(--line);
          border-radius: var(--radius-lg);
          padding: 0.8rem 0.9rem;
          background: linear-gradient(170deg, var(--panel) 0%, var(--panel-soft) 100%);
          background: linear-gradient(170deg, color-mix(in srgb, var(--panel) 94%, transparent), color-mix(in srgb, var(--panel-soft) 94%, transparent));
          box-shadow: var(--shadow);
        }

        .metric-value {
          display: block;
          font-family: var(--font-display);
          font-size: 1.28rem;
          font-weight: 700;
        }

        .metric-label {
          display: block;
          margin-top: 0.1rem;
          font-size: 0.84rem;
          color: var(--muted);
          text-transform: uppercase;
          letter-spacing: 0.06em;
        }

        .cards-grid {
          display: grid;
          gap: 0.85rem;
          grid-template-columns: repeat(auto-fill, minmax(245px, 1fr));
        }

        .type-card,
        .spec-link {
          text-decoration: none;
          color: inherit;
          border: 1px solid var(--line);
          border-radius: var(--radius-lg);
          background: linear-gradient(155deg, var(--panel) 0%, var(--panel-soft) 100%);
          background: linear-gradient(155deg, color-mix(in srgb, var(--panel) 95%, transparent), color-mix(in srgb, var(--panel-soft) 96%, transparent));
          padding: 0.95rem;
          box-shadow: var(--shadow);
          transition: transform 120ms ease, box-shadow 120ms ease;
          position: relative;
          overflow: hidden;
        }

        .type-card::before,
        .spec-link::before {
          content: "";
          position: absolute;
          inset: 0 auto auto 0;
          width: 100%;
          height: 3px;
          background: linear-gradient(90deg, var(--brand), var(--accent));
          opacity: 0.72;
        }

        .type-card:hover,
        .spec-link:hover {
          transform: translateY(-2px) scale(1.004);
          box-shadow: 0 18px 35px rgba(11, 95, 115, 0.2);
          box-shadow: 0 18px 35px color-mix(in srgb, var(--brand) 20%, transparent);
        }

        .type-card-header,
        .spec-link-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 0.6rem;
        }

        .type-card h2,
        .spec-link h3 {
          margin: 0;
          font-size: 1.02rem;
          font-family: var(--font-display);
        }

        .type-card-meta,
        .spec-link-meta {
          margin: 0.4rem 0 0;
          color: var(--muted);
          font-size: 0.9rem;
        }

        .pill {
          border-radius: 999px;
          padding: 0.13rem 0.54rem;
          background: var(--brand-soft);
          background: color-mix(in srgb, var(--brand-soft) 85%, transparent);
          color: var(--brand);
          font-size: 0.84rem;
          font-weight: 700;
          border: 1px solid rgba(10, 95, 115, 0.2);
          border: 1px solid color-mix(in srgb, var(--brand) 28%, transparent);
        }

        .status {
          display: inline-block;
          border-radius: 999px;
          padding: 0.15rem 0.5rem;
          font-size: 0.78rem;
          border: 1px solid transparent;
          text-transform: lowercase;
        }

        .status-active {
          color: var(--ok);
          background: rgba(29, 122, 67, 0.15);
          border-color: rgba(29, 122, 67, 0.35);
          background: color-mix(in srgb, var(--ok) 15%, transparent);
          border-color: color-mix(in srgb, var(--ok) 35%, transparent);
        }

        .status-deprecated {
          color: var(--warn);
          background: rgba(146, 93, 0, 0.16);
          border-color: rgba(146, 93, 0, 0.38);
          background: color-mix(in srgb, var(--warn) 16%, transparent);
          border-color: color-mix(in srgb, var(--warn) 38%, transparent);
        }

        .status-retired {
          color: var(--danger);
          background: rgba(155, 35, 35, 0.16);
          border-color: rgba(155, 35, 35, 0.38);
          background: color-mix(in srgb, var(--danger) 16%, transparent);
          border-color: color-mix(in srgb, var(--danger) 38%, transparent);
        }

        .status-draft {
          color: var(--draft);
          background: rgba(101, 101, 101, 0.14);
          border-color: rgba(101, 101, 101, 0.34);
          background: color-mix(in srgb, var(--draft) 14%, transparent);
          border-color: color-mix(in srgb, var(--draft) 34%, transparent);
        }

        .spec-summary,
        .spec-section {
          border: 1px solid var(--line);
          border-radius: var(--radius-lg);
          background: var(--panel);
          padding: 0.95rem 1rem 1rem;
          margin-top: 0.82rem;
          box-shadow: var(--shadow);
          box-shadow: var(--shadow), var(--surface-shadow);
        }

        .spec-section h2 {
          margin: 0 0 0.65rem;
          font-family: var(--font-display);
          font-size: 1.1rem;
          color: var(--text);
        }

        .spec-summary dl,
        .yaml-map {
          margin: 0;
          display: grid;
          grid-template-columns: minmax(130px, 220px) 1fr;
          gap: 0.45rem 0.8rem;
        }

        .spec-summary dt,
        .yaml-map dt {
          font-weight: 700;
          color: var(--text);
          color: color-mix(in srgb, var(--text) 82%, var(--brand));
        }

        .spec-summary dd,
        .yaml-map dd {
          margin: 0;
        }

        .yaml-sequence {
          margin: 0;
          padding-left: 1.2rem;
        }

        .yaml-sequence li {
          margin: 0.35rem 0;
        }

        .yaml-source,
        .scalar-block {
          margin: 0;
          border-radius: 12px;
          border: 1px solid var(--line);
          border: 1px solid color-mix(in srgb, var(--line) 86%, transparent);
          background: var(--bg-alt);
          background: color-mix(in srgb, var(--bg-alt) 70%, var(--panel));
          padding: 0.8rem;
          overflow-x: auto;
          font-size: 0.88rem;
          line-height: 1.45;
          font-family: var(--font-mono);
        }

        .empty {
          margin: 0;
          color: var(--muted);
          font-style: italic;
        }

        code {
          font-family: var(--font-mono);
        }

        .spec-list {
          display: grid;
          gap: 0.7rem;
        }

        @media (max-width: 980px) {
          .shell {
            grid-template-columns: 1fr;
          }

          .sidebar {
            position: static;
            height: auto;
            border-right: 0;
            border-bottom: 1px solid var(--line);
            backdrop-filter: none;
          }

          .spec-summary dl,
          .yaml-map {
            grid-template-columns: 1fr;
          }

          .sidebar-controls {
            grid-template-columns: 1fr;
          }
        }

        @media (max-width: 560px) {
          .content {
            padding: 1rem 0.7rem 1.4rem;
          }

          .hero {
            padding: 1rem;
          }
        }
        """;
}
