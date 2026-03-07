# Demo: Spinner Styles
# Shows the /spinner command and different progress indicator styles

Output docs/images/demo-spinner.gif

Set Shell "bash"
Set FontSize 14
Set Width 1200
Set Height 600
Set Theme "Dracula"
Set TypingSpeed 50ms
Set Padding 20

Type "jdai --new"
Enter
Sleep 5s

# Show current spinner style
Type "/spinner"
Enter
Sleep 2s

Screenshot docs/images/demo-spinner-default.png

# Switch to minimal style
Type "/spinner minimal"
Enter
Sleep 2s

Screenshot docs/images/demo-spinner-minimal.png

# Switch to rich style
Type "/spinner rich"
Enter
Sleep 2s

Screenshot docs/images/demo-spinner-rich.png

# Switch to nerdy style
Type "/spinner nerdy"
Enter
Sleep 2s

Screenshot docs/images/demo-spinner-nerdy.png

# Reset to normal
Type "/spinner normal"
Enter
Sleep 2s

Screenshot docs/images/demo-spinner-normal.png
Sleep 2s
