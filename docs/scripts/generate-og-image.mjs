#!/usr/bin/env node
// Generates the Open Graph social preview image (1200x630) for link embeds.
// Usage: node docs/scripts/generate-og-image.mjs

import { chromium } from 'playwright';
import { mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const imagesDir = join(__dirname, '..', 'images');
mkdirSync(imagesDir, { recursive: true });

const html = `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<style>
  @import url('https://fonts.googleapis.com/css2?family=Instrument+Sans:wght@400;600;700&family=JetBrains+Mono:wght@400;700&display=swap');
  * { margin:0; padding:0; box-sizing:border-box; }
  body {
    width: 1200px; height: 630px;
    background: #0d1117;
    font-family: 'Instrument Sans', system-ui, sans-serif;
    color: #e6edf3;
    display: flex;
    overflow: hidden;
    position: relative;
  }

  /* Subtle radial gradient glow */
  body::before {
    content: '';
    position: absolute;
    top: -120px; right: -80px;
    width: 600px; height: 600px;
    background: radial-gradient(circle, rgba(88,166,255,0.12) 0%, transparent 70%);
    pointer-events: none;
  }
  body::after {
    content: '';
    position: absolute;
    bottom: -100px; left: 80px;
    width: 400px; height: 400px;
    background: radial-gradient(circle, rgba(121,192,255,0.08) 0%, transparent 70%);
    pointer-events: none;
  }

  .container {
    display: flex;
    width: 100%;
    height: 100%;
    padding: 60px 72px;
    flex-direction: column;
    justify-content: space-between;
    position: relative;
    z-index: 1;
  }

  .top {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
  }

  .brand {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  h1 {
    font-size: 72px;
    font-weight: 700;
    letter-spacing: -2px;
    background: linear-gradient(135deg, #e6edf3 0%, #58a6ff 50%, #79c0ff 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    line-height: 1;
  }

  .tagline {
    font-size: 24px;
    color: #8b949e;
    font-weight: 400;
    max-width: 600px;
    line-height: 1.4;
  }

  .terminal-preview {
    background: #161b22;
    border: 1px solid #30363d;
    border-radius: 12px;
    padding: 16px 20px;
    font-family: 'JetBrains Mono', monospace;
    font-size: 14px;
    line-height: 1.7;
    color: #e6edf3;
    max-width: 520px;
    box-shadow: 0 8px 24px rgba(0,0,0,0.4);
  }

  .terminal-preview .dots {
    display: flex;
    gap: 6px;
    margin-bottom: 12px;
  }
  .terminal-preview .dot {
    width: 10px; height: 10px;
    border-radius: 50%;
  }
  .dot-r { background: #ff5f56; }
  .dot-y { background: #ffbd2e; }
  .dot-g { background: #27c93f; }

  .dim { color: #6e7681; }
  .green { color: #3fb950; }
  .cyan { color: #58a6ff; }
  .purple { color: #bc8cff; }
  .pink { color: #f778ba; }

  .bottom {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  .features {
    display: flex;
    gap: 32px;
  }

  .feature {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 15px;
    color: #8b949e;
  }

  .feature .icon {
    color: #58a6ff;
    font-size: 18px;
    font-weight: 700;
  }

  .install-cmd {
    font-family: 'JetBrains Mono', monospace;
    font-size: 14px;
    color: #58a6ff;
    background: rgba(88,166,255,0.08);
    border: 1px solid rgba(88,166,255,0.2);
    border-radius: 8px;
    padding: 8px 16px;
  }

  .border-accent {
    position: absolute;
    bottom: 0; left: 0; right: 0;
    height: 3px;
    background: linear-gradient(90deg, #58a6ff 0%, #79c0ff 50%, #bc8cff 100%);
  }
</style></head>
<body>
  <div class="container">
    <div class="top">
      <div class="brand">
        <h1>JD.AI</h1>
        <div class="tagline">
          AI-powered terminal assistant built on<br>
          Microsoft Semantic Kernel
        </div>
      </div>
      <div class="terminal-preview">
        <div class="dots">
          <div class="dot dot-r"></div>
          <div class="dot dot-y"></div>
          <div class="dot dot-g"></div>
        </div>
        <div><span class="dim">$</span> jdai</div>
        <div><span class="green">✅</span> Claude Code: Authenticated</div>
        <div><span class="green">✅</span> GitHub Copilot: 3 models</div>
        <div><span class="green">✅</span> Ollama: 59 models</div>
        <div>&nbsp;</div>
        <div><span class="purple">❯</span> <span class="cyan">explore the architecture</span></div>
      </div>
    </div>
    <div class="bottom">
      <div class="features">
        <div class="feature"><span class="icon">◆</span> Multi-provider</div>
        <div class="feature"><span class="icon">◆</span> Tool execution</div>
        <div class="feature"><span class="icon">◆</span> Subagent swarms</div>
        <div class="feature"><span class="icon">◆</span> Team orchestration</div>
      </div>
      <div class="install-cmd">dotnet tool install -g JD.AI</div>
    </div>
  </div>
  <div class="border-accent"></div>
</body></html>`;

async function main() {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1200, height: 630 } });
  await page.setContent(html, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);

  const outPath = join(imagesDir, 'og-social.png');
  await page.screenshot({ path: outPath, clip: { x: 0, y: 0, width: 1200, height: 630 } });
  console.log(`✅ og-social.png (1200×630)`);

  await browser.close();
}

main().catch(e => { console.error(e); process.exit(1); });
