// Licensed under the MIT License.

namespace JD.AI.Core.Attributes;

/// <summary>
/// Marks a class as a tool plugin for automatic discovery and registration.
/// Classes decorated with this attribute are scanned at startup and registered
/// with the Semantic Kernel without requiring manual <c>AddFromType</c> calls.
/// </summary>
/// <example>
/// <code>
/// [ToolPlugin("file", Description = "File system operations")]
/// public sealed class FileTools { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolPluginAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="ToolPluginAttribute"/> with the specified plugin name.
    /// </summary>
    /// <param name="name">
    /// The plugin name used for Semantic Kernel registration
    /// (e.g., "file", "git", "shell").
    /// </param>
    public ToolPluginAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Gets the plugin registration name.</summary>
    public string Name { get; }

    /// <summary>Gets or sets a human-readable description of the plugin.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this plugin requires constructor injection
    /// (i.e., cannot be registered via <c>AddFromType</c> and needs <c>AddFromObject</c>).
    /// When true, the assembly scanner will skip auto-registration and the plugin
    /// must be registered explicitly with its dependencies.
    /// </summary>
    public bool RequiresInjection { get; set; }

    /// <summary>
    /// Gets or sets the order in which this plugin should be registered.
    /// Lower values are registered first. Default is 0.
    /// </summary>
    public int Order { get; set; }
}
