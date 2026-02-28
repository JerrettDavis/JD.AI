# Demo: Session Management
# Shows /name, /save, /sessions, /cost

Output docs/images/demo-sessions.gif

Set Shell "pwsh"
Set FontSize 14
Set Width 1200
Set Height 600
Set Theme "Dracula"
Set TypingSpeed 50ms
Set Padding 20

Type "jdai --new"
Enter
Sleep 5s

Type "Hello! This is a demo session."
Enter
Sleep 8s

Type "/name demo-session"
Enter
Sleep 2s

Screenshot docs/images/demo-sessions-name.png

Type "/cost"
Enter
Sleep 2s

Screenshot docs/images/demo-sessions-cost.png

Type "/sessions"
Enter
Sleep 3s

Screenshot docs/images/demo-sessions-list.png
Sleep 2s
