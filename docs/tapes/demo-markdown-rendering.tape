# Demo: Markdown Rendering, Diff View, and Slash-Command Highlighting
# Demonstrates the new TUI markdown renderer added in feat/tui-markdown-rendering.
# Produces a deterministic recording — no live model needed.

Output docs/images/demo-markdown-rendering.gif

Set Shell "powershell"
Set FontSize 14
Set Width 1280
Set Height 900
Set Theme "Dracula"
Set TypingSpeed 25ms
Set Padding 20

# Run the pre-published standalone demo executable
Type "C:\git\JD.AI\docs\demo\publish\MarkdownDemo.exe"
Enter
Sleep 4s

# Capture the welcome + markdown response section
Screenshot docs/images/demo-markdown-rendering.png

Sleep 1s

# Capture the full output including diff + slash commands
Screenshot docs/images/demo-diff-rendering.png

Sleep 2s
