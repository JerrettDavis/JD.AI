using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Governance;
using JD.AI.Tests.Fixtures;
using Xunit;

namespace JD.AI.Tests.Governance;

public sealed class PolicyLoaderTests : IDisposable
{
    private readonly TempDirectoryFixture _tempDir = new();
    private readonly string _globalPoliciesDir;
    private readonly string _projectPoliciesDir;
    private readonly string _orgConfigDir;

    public PolicyLoaderTests()
    {
        // PolicyLoader.Load looks at: {DataDirectories.Root}/policies/
        // So SetRoot must point to the .jdai root (not the parent of .jdai)
        _globalPoliciesDir = _tempDir.CreateSubdirectory("policies");
        // For project: PolicyLoader uses {projectPath}/.jdai/policies
        _projectPoliciesDir = _tempDir.CreateSubdirectory("project/.jdai/policies");
        // For org: JDAI_ORG_CONFIG env var points to the org dir,
        // and PolicyLoader uses {orgConfigPath}/policies
        _orgConfigDir = _tempDir.CreateSubdirectory("org-config");
        // Create the policies subdirectory inside org-config
        _tempDir.CreateSubdirectory("org-config/policies");
    }

    public void Dispose() => _tempDir.Dispose();

    private static string MinimalPolicy(string name, string scope = "User") => $$"""
        apiVersion: jdai/v1
        kind: Policy
        metadata:
          name: {{name}}
          scope: {{scope}}
          priority: 10
        spec: {}
        """;

    private void WritePolicyToGlobal(string filename, string content)
    {
        File.WriteAllText(Path.Combine(_globalPoliciesDir, filename), content);
    }

    private void WritePolicyToProject(string filename, string content)
    {
        File.WriteAllText(Path.Combine(_projectPoliciesDir, filename), content);
    }

    private void WritePolicyToOrg(string filename, string content)
    {
        File.WriteAllText(Path.Combine(_orgConfigDir, "policies", filename), content);
    }

    [Fact]
    public void Load_WithNoProjectPath_LoadsGlobalPoliciesOnly()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToGlobal("global.yaml", MinimalPolicy("global-policy"));

        // Act
        var policies = PolicyLoader.Load(projectPath: null);

        // Assert
        policies.Should().HaveCount(1);
        policies[0].Metadata.Name.Should().Be("global-policy");
        policies[0].Metadata.Scope.Should().Be(PolicyScope.User);

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_WithProjectPath_LoadsGlobalAndProjectPolicies()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToGlobal("global.yaml", MinimalPolicy("global-policy", "User"));
        WritePolicyToProject("project.yaml", MinimalPolicy("project-policy", "User"));

        var projectPath = Path.Combine(_tempDir.DirectoryPath, "project");

        // Act
        var policies = PolicyLoader.Load(projectPath);

        // Assert
        policies.Should().HaveCount(2);
        var names = policies.Select(p => p.Metadata.Name).ToList();
        names.Should().Contain(["global-policy", "project-policy"]);

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_ProjectLevelUserScope_PromotedToProjectScope()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToProject("project.yaml", MinimalPolicy("project-policy", "User"));

        var projectPath = Path.Combine(_tempDir.DirectoryPath, "project");

        // Act
        var policies = PolicyLoader.Load(projectPath);

        // Assert
        var projectPolicy = policies.First(p => string.Equals(p.Metadata.Name, "project-policy", StringComparison.Ordinal));
        projectPolicy.Metadata.Scope.Should().Be(PolicyScope.Project);

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_OrgLevelUserScope_PromotedToOrganizationScope()
    {
        // Arrange
        WritePolicyToOrg("org.yaml", MinimalPolicy("org-policy", "User"));

        // Set org config path BEFORE setting root so OrgConfigPath resolves correctly
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgConfigDir);
        DataDirectories.SetRoot(_tempDir.DirectoryPath);

        try
        {
            // Act
            var policies = PolicyLoader.Load(projectPath: null);

            // Assert
            var orgPolicy = policies.FirstOrDefault(p => string.Equals(p.Metadata.Name, "org-policy", StringComparison.Ordinal));
            orgPolicy.Should().NotBeNull();
            orgPolicy!.Metadata.Scope.Should().Be(PolicyScope.Organization);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", null);
            DataDirectories.Reset();
        }
    }

    [Fact]
    public void Load_OrgConfigNotSet_SkipsOrgPolicies()
    {
        // Arrange
        // Ensure JDAI_ORG_CONFIG is not set, then set root
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", null);
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToGlobal("global.yaml", MinimalPolicy("global-policy"));
        WritePolicyToOrg("org.yaml", MinimalPolicy("org-policy"));

        // Act
        var policies = PolicyLoader.Load(projectPath: null);

        // Assert
        policies.Should().HaveCount(1);
        policies[0].Metadata.Name.Should().Be("global-policy");

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_NonExistentGlobalDir_ReturnsEmpty()
    {
        // Arrange
        var nonExistentRoot = Path.Combine(_tempDir.DirectoryPath, "nonexistent");
        DataDirectories.SetRoot(nonExistentRoot);

        // Act
        var policies = PolicyLoader.Load(projectPath: null);

        // Assert
        policies.Should().BeEmpty();

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_MalformedYaml_SkippedSilently()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToGlobal("good.yaml", MinimalPolicy("good-policy"));
        WritePolicyToGlobal("bad.yaml", "bad:\n  yaml:\n    nope:\n  oops");

        // Act
        var policies = PolicyLoader.Load(projectPath: null);

        // Assert
        policies.Should().HaveCount(1);
        policies[0].Metadata.Name.Should().Be("good-policy");

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_SortOrder_ScopeFirst_ThenPriority()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);

        // Global policy with priority 50
        WritePolicyToGlobal("global-low.yaml", $$"""
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: global-low-priority
              scope: Global
              priority: 50
            spec: {}
            """);

        // Global policy with priority 10
        WritePolicyToGlobal("global-high.yaml", $$"""
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: global-high-priority
              scope: Global
              priority: 10
            spec: {}
            """);

        // User policy with priority 20
        WritePolicyToGlobal("user-mid.yaml", $$"""
            apiVersion: jdai/v1
            kind: Policy
            metadata:
              name: user-priority
              scope: User
              priority: 20
            spec: {}
            """);

        // Act
        var policies = PolicyLoader.Load(projectPath: null);

        // Assert
        policies.Should().HaveCount(3);

        // Global scope (lower enum value) should come first, then sorted by priority
        policies[0].Metadata.Scope.Should().Be(PolicyScope.Global);
        policies[0].Metadata.Priority.Should().Be(10);

        policies[1].Metadata.Scope.Should().Be(PolicyScope.Global);
        policies[1].Metadata.Priority.Should().Be(50);

        policies[2].Metadata.Scope.Should().Be(PolicyScope.User);
        policies[2].Metadata.Priority.Should().Be(20);

        DataDirectories.Reset();
    }

    [Fact]
    public void Load_MultipleFilesInEachLevel_AllLoaded()
    {
        // Arrange
        DataDirectories.SetRoot(_tempDir.DirectoryPath);
        WritePolicyToGlobal("policy1.yaml", MinimalPolicy("policy1"));
        WritePolicyToGlobal("policy2.yaml", MinimalPolicy("policy2"));
        WritePolicyToGlobal("policy3.yml", MinimalPolicy("policy3"));

        var projectPath = Path.Combine(_tempDir.DirectoryPath, "project");

        // Act
        var policies = PolicyLoader.Load(projectPath);

        // Assert
        policies.Should().HaveCount(3);
        var names = policies.Select(p => p.Metadata.Name).ToList();
        names.Should().Contain(["policy1", "policy2", "policy3"]);

        DataDirectories.Reset();
    }
}
