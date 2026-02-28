# Demo: Team Orchestration
# Shows a sequential team with progress panel

Output docs/images/demo-orchestration.gif

Set Shell "pwsh"
Set FontSize 14
Set Width 1200
Set Height 700
Set Theme "Dracula"
Set TypingSpeed 25ms
Set Padding 20

Type "jdai --new --dangerously-skip-permissions"
Enter
Sleep 5s

Type "Use a sequential team: first an explore agent to analyze the codebase architecture, then a plan agent to suggest improvements"
Sleep 500ms
Enter
Sleep 30s

Screenshot docs/images/demo-orchestration.png
Sleep 2s
