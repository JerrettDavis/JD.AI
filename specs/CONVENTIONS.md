# OpenClaw UI Spec Conventions

## File Naming
- kebab-case matching nav path: chat.md, control-overview.md, agent-agents.md, settings-config.md
- Global: navigation-sidebar.md

## Section Template (use for every spec file)

# [Page Title]

**Route:** `/path/to/page`  
**Nav Path:** Sidebar > Section > Page  
**Description:** One-sentence summary of page purpose.

## Layout
Describe the overall page layout (panels, columns, split views, etc.)

## Components
List each UI component with:
- **Component name** — type (button/input/table/card/etc.), label, behavior, data displayed

## Interactions
List user interactions: click X → does Y, form submission behavior, modals triggered, etc.

## State / Data
What data is loaded on page, where it comes from, loading/empty/error states.

## API / WebSocket Calls
Any known API endpoints or WS messages triggered by this page.

## Notes
Edge cases, quirks, or things that need follow-up.
