# Demo: Thinking Progress Indicator
# Shows the animated spinner during an LLM turn with the normal style,
# then the styled turn metrics line after completion

Output docs/images/demo-thinking.gif

Set Shell "bash"
Set FontSize 14
Set Width 1200
Set Height 600
Set Theme "Dracula"
Set TypingSpeed 30ms
Set Padding 20

Type "jdai --new"
Enter
Sleep 5s

# Send a message — the spinner should appear while the model thinks
Type "What is 2 + 2?"
Sleep 500ms
Enter
Sleep 3s

# Capture the spinner in action (cyan braille dots with elapsed time)
Screenshot docs/images/demo-thinking-spinner.png

# Wait for the response to complete
Sleep 12s

# Capture the completed turn with metrics line
Screenshot docs/images/demo-thinking-metrics.png
Sleep 2s
