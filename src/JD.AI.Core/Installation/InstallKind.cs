namespace JD.AI.Core.Installation;

/// <summary>
/// How JD.AI was installed on the current machine.
/// </summary>
public enum InstallKind
{
    /// <summary>.NET global tool (<c>dotnet tool install -g JD.AI</c>).</summary>
    DotnetTool,

    /// <summary>Self-contained native binary downloaded from GitHub Releases.</summary>
    NativeBinary,

    /// <summary>Windows Package Manager (<c>winget install JD.AI</c>).</summary>
    Winget,

    /// <summary>Chocolatey (<c>choco install jdai</c>).</summary>
    Chocolatey,

    /// <summary>Scoop (<c>scoop install jdai</c>).</summary>
    Scoop,

    /// <summary>Homebrew (<c>brew install jdai</c>).</summary>
    Brew,

    /// <summary>APT / dpkg (<c>apt install jdai</c>).</summary>
    Apt,

    /// <summary>Unable to determine installation method.</summary>
    Unknown,
}
