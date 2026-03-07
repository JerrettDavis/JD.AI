# CI smoke tape for non-subscription CLI subcommands.
# Exercises commands that do not require provider auth or model execution.

Output docs/images/ci-cli-smoke.gif

Set Shell "bash"
Set Width 1200
Set Height 700
Set FontSize 14
Set TypingSpeed 0ms
Set Padding 20
Set Theme "Dracula"

Type "./artifacts/vhs-smoke/JD.AI mcp --help"
Enter
Sleep 3s
Screenshot docs/images/ci-mcp-help.png

Type "./artifacts/vhs-smoke/JD.AI plugin --help"
Enter
Sleep 3s
Screenshot docs/images/ci-plugin-help.png
