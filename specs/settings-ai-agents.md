# Settings > Providers & Agents
n> **Verified:** Live UI [2026-04-11] via gateway token


**Route:** `/settings`  
**Nav Path:** Sidebar > Settings > (Providers tab / Agents tab)  
**Description:** Configure AI providers and agent definitions. Providers supply models; agents consume them with customizable parameters and system prompts.

## Layout

The Settings page uses a tabbed interface with multiple configuration sections. The **Providers** and **Agents** tabs are two of six tabs available under Settings (Server, Providers, Agents, Channels, Routing, OpenClaw).

### Providers Tab

Horizontal card layout with one card per AI provider. Each card displays:
- Provider name and description (left-aligned)
- Enable toggle (center)
- Test button (blue, right-aligned)
- Conditional: If enabled and models loaded, a dense table showing model ID and display name

### Agents Tab

Stacked card layout with one expandable card per agent definition. Each card contains:
- Agent ID and delete button (header row)
- Basic fields: ID, Provider select, Model select, Max Turns, Auto-Spawn toggle
- System Prompt textarea
- Collapsible "Model Parameters" panel (Temperature, Top-P, Top-K, Max Tokens, Context Window, Frequency Penalty, Presence Penalty, Repeat Penalty, Seed, Stop Sequences)
- Bottom: Add Agent button, Save Agents button

## Components

### Providers Tab

**Provider Cards** — Card element for each provider
- **Status Avatar** — Color indicator: green if enabled, default if disabled
- **Provider Name** — Subtitle, bold text
- **Provider Description** — Caption text (read-only, derived from provider type)
- **Enabled Toggle** — Boolean switch, triggers state change
- **Test Button** — Icon button labeled "Network Check", tests connectivity
- **Models Table** (conditional) — Displayed when enabled and models available
  - Columns: Model ID (monospace), Display Name
  - Rows: List of available models from provider
- **Test Result Alert** (conditional) — Green (success) or red (error) alert showing test message

**Save Button** — Bottom-right, filled primary color, saves provider configuration

### Agents Tab

**Add Agent** — Outlined primary button, adds new agent definition to list

**Agent Cards** — One per agent, stacked layout
- **Header Row**
  - **Agent ID Display** — Subtitle, shows "New Agent" if blank, otherwise the ID
  - **Delete Button** — Red icon button, removes agent from list
- **Basic Configuration Grid**
  - **Agent ID** (xs=12, md=6) — Text input, required, helper text "Unique identifier (e.g. 'default', 'code-reviewer')"
  - **Provider Select** (xs=12, md=6) — Dropdown, populates from enabled providers, helper text "The AI provider to use"
  - **Model Select** (xs=12, md=6) — Dynamic dropdown; if provider is selected and has models, shows model list; otherwise text input
  - **Max Turns** (xs=12, md=3) — Numeric input, min=0, max=100, helper text "Max conversation turns (0 = unlimited)"
  - **Auto-Spawn Toggle** (xs=12, md=3) — Boolean switch, helper text "Start on boot"
  - **System Prompt** (xs=12) — Textarea, 3 rows, helper text "Instructions that define agent personality and capabilities"

- **Model Parameters Panel** (collapsible)
  - **Temperature** (xs=6, md=3) — Numeric, 0.0–2.0, step 0.05, format F2, clearable, helper "0 = deterministic, 2 = very creative"
  - **Top-P** (xs=6, md=3) — Numeric, 0.0–1.0, step 0.05, clearable, helper "Nucleus sampling threshold"
  - **Top-K** (xs=6, md=3) — Numeric, 1–200, clearable, helper "Limit to K most probable tokens"
  - **Max Tokens** (xs=6, md=3) — Numeric, 1–131072, clearable, helper "Max tokens in response"
  - **Context Window** (xs=6, md=3) — Numeric, 0–1048576, clearable, helper "Ollama num_ctx (0 = default)"
  - **Frequency Penalty** (xs=6, md=3) — Numeric, -2.0–2.0, step 0.1, clearable, helper "Penalize repeated tokens"
  - **Presence Penalty** (xs=6, md=3) — Numeric, -2.0–2.0, step 0.1, clearable, helper "Encourage new topics"
  - **Repeat Penalty** (xs=6, md=3) — Numeric, 0.0–3.0, step 0.1, clearable, helper "Ollama repeat_penalty (1.0 = off)"
  - **Seed** (xs=6, md=3) — Numeric, min 0, clearable, helper "For reproducible output"
  - **Stop Sequences** (xs=6, md=9) — Text input, comma-separated, helper "e.g. &lt;|end|&gt;, [DONE]"

**Save Agents Button** — Bottom-right, filled primary color, saves all agent definitions

## Interactions

### Providers Tab

1. **Enable/Disable Provider** — Toggle switch changes `Enabled` state instantly
2. **Test Provider** — Click "Test" button → Calls `GetProviderModelsAsync(name)`
   - Success: Models table appears below, showing available models
   - Error: Red alert appears with error message
3. **Save Configuration** — Click "Save Providers" → Calls `UpdateProvidersConfigAsync(Providers)`
   - Success: Green snackbar "Provider configuration saved"
   - Error: Red snackbar with error message

### Agents Tab

1. **Add Agent** — Click "Add Agent Definition" → Appends new agent to list with `AutoSpawn = true`
2. **Delete Agent** — Click red delete button → Removes agent from list, re-indexes
3. **Provider Change** — When provider is selected, triggers load of available models for that provider
   - If models available, Model field becomes dropdown
   - Otherwise, text input field
4. **Save Agents** — Click "Save Agents" → Syncs stop sequences from text input to model, calls `UpdateAgentsConfigAsync(Agents)`
   - Success: Green snackbar "Agent definitions saved"
   - Error: Red snackbar with error message

## State / Data

### Providers Tab

**Data Loaded On Page Init:**
- `Providers` — List of `ProviderConfigModel` objects (Name, Enabled, ...)
- `_providerModels` — Dictionary mapping provider name to `ProviderModelInfo[]` (populated on test)

**Model Availability:**
- Initially empty; populated when Test button is clicked
- Resets if provider is disabled and re-enabled

### Agents Tab

**Data Loaded On Page Init:**
- `Agents` — List of `AgentDefinition` objects (Id, Provider, Model, MaxTurns, AutoSpawn, SystemPrompt, Parameters, ...)
- `AvailableProviders` — List of `ProviderConfigModel` objects (used for Provider dropdown)
- `_providerModels` — Dictionary of models per provider (loaded on component init for enabled providers)
- `_stopSeqText` — Dictionary mapping agent index to comma-separated stop-sequence text (synced on save)

**Parameters Object Properties:**
- Temperature, TopP, TopK, MaxTokens, ContextWindowSize, FrequencyPenalty, PresencePenalty, RepeatPenalty, Seed, StopSequences[]

**Loading/Empty States:**
- Agents tab: No empty state; at least one agent card displayed initially
- If no providers enabled, Provider dropdown is empty

**Error States:**
- Provider test failure: Red alert with error message
- Save failure: Red snackbar with exception message

## API / WebSocket Calls

**Providers Tab:**
- `GET /api/gateway/config` — Fetch provider configurations on page init (called via `GetConfigAsync()`)
- `POST /api/gateway/providers/test?name={providerName}` — Test provider connectivity, fetch models (called via `GetProviderModelsAsync(name)`)
- `PUT /api/gateway/providers` — Save provider configuration (called via `UpdateProvidersConfigAsync(Providers)`)

**Agents Tab:**
- `GET /api/gateway/config` — Fetch agent definitions on page init
- `POST /api/gateway/providers/models?name={providerName}` — Fetch models for provider (called on provider change)
- `PUT /api/gateway/agents` — Save agent definitions (called via `UpdateAgentsConfigAsync(Agents)`)

## Notes

- **Model Parameters Default Behavior:** If a parameter field is cleared/left blank, the provider's default value is used
- **Stop Sequences Format:** User inputs comma-separated text; internally stored as string array; synced on save
- **Provider Models Caching:** Models are cached in `_providerModels` dict; reused across agent definitions
- **Agent ID Uniqueness:** No client-side validation; server likely enforces uniqueness
- **Max Turns = 0:** Interpreted as unlimited conversation turns
- **Auto-Spawn Flag:** Agents with `AutoSpawn = true` start automatically when gateway boots
- **System Prompt:** Free-form text; no length limit visible in UI
- **Context Window (Ollama):** Specific to Ollama provider; other providers ignore this field
- **Test Result Persistence:** Test results remain visible in UI until page refresh or provider toggle change
- **Saving Disabled During Request:** Save button becomes disabled while request is in flight (shows loading spinner)
