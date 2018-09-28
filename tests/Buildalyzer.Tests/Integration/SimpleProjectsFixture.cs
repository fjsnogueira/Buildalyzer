﻿using Buildalyzer.Environment;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Buildalyzer.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    public class SimpleProjectsFixture
    {
        private const LoggerVerbosity Verbosity = LoggerVerbosity.Normal;
        private const bool BinaryLog = false;

        private static EnvironmentPreference[] Preferences =
        {
#if Is_Windows
            EnvironmentPreference.Framework,
#endif
            EnvironmentPreference.Core
        };

        private static string[] ProjectFiles =
        {
#if Is_Windows
            @"LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
            @"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj",
            @"SdkFrameworkProject\SdkFrameworkProject.csproj",
#endif
            @"SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"SdkNetCoreProjectWithReference\SdkNetCoreProjectWithReference.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj",
            @"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj",
            @"SdkProjectWithImportedProps\SdkProjectWithImportedProps.csproj",
            @"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj"
        };

        [Test]
        public void DesignTimeBuildsProject(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void BuildsProject(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference,
                DesignTime = false
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.OverallSuccess.ShouldBeTrue(log.ToString());
            results.ShouldAllBe(x => x.Succeeded, log.ToString());
        }

        [Test]
        public void GetsSourceFiles(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build(options).First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').TakeLast(2).First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void GetsReferences(
            [ValueSource(nameof(Preferences))] EnvironmentPreference preference,
            [ValueSource(nameof(ProjectFiles))] string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            IReadOnlyList<string> references = analyzer.Build(options).First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("mscorlib.dll"), log.ToString());
            if (projectFile.Contains("PackageReference"))
            {
                references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
            }
        }

        [Test]
        public void MultiTargetingBuildAllTargetFrameworksGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            AnalyzerResults results = analyzer.Build();

            // Then
            results.Count.ShouldBe(2);
            results.TargetFrameworks.ShouldBe(new[] { "net462", "netstandard2.0" }, true, log.ToString());
            results["net462"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
            results["netstandard2.0"].SourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void MultiTargetingBuildFrameworkTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("net462").First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void MultiTargetingBuildCoreTargetFrameworkGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("netstandard2.0").First().SourceFiles;

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void SdkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void SdkProjectWithProjectReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetCoreProjectWithReference\SdkNetCoreProjectWithReference.csproj", log);

            // When
            IEnumerable<string> references = analyzer.Build().First().ProjectReferences;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProjectWithPackageReference.csproj"), log.ToString());
            references.ShouldContain(x => x.EndsWith("SdkNetStandardProject.csproj"), log.ToString());
        }

#if Is_Windows
        [Test]
        public void LegacyFrameworkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.Build().First().References;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void LegacyFrameworkProjectWithProjectReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj", log);

            // When
            IEnumerable<string> references = analyzer.Build().First().ProjectReferences;

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("LegacyFrameworkProject.csproj"), log.ToString());
            references.ShouldContain(x => x.EndsWith("LegacyFrameworkProjectWithPackageReference.csproj"), log.ToString());
        }
#endif

        [Test]
        public void GetsProjectsInSolution()
        {
            // Given
            StringWriter log = new StringWriter();

            // When
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                });

            // Then
            ProjectFiles.Select(x => GetProjectPath(x)).ShouldBeSubsetOf(manager.Projects.Keys, log.ToString());
        }

        [Test]
        public void IgnoreSolutionItemsThatAreNotProjects()
        {
            // Given / When
            AnalyzerManager manager = new AnalyzerManager(GetProjectPath("TestProjects.sln"));

            // Then
            manager.Projects.Any(x => x.Value.ProjectFile.Path.Contains("TestEmptySolutionFolder")).ShouldBeFalse();
        }

        [Test]
        public void GetsProjectGuidFromSolution([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"));
            ProjectAnalyzer analyzer = manager.Projects.First(x => x.Key.EndsWith("SdkNetStandardProject.csproj")).Value;
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "obj");
            DeleteProjectDirectory(analyzer.ProjectFile.Path, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.First().ProjectGuid.ToString().ShouldBe("016713d9-b665-4272-9980-148801a9b88f");
        }

        [Test]
        public void GetsProjectGuidFromProject([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            string projectFile = @"SdkNetCoreProject\SdkNetCoreProject.csproj";
            ProjectAnalyzer analyzer = new AnalyzerManager()
                .GetProject(GetProjectPath(projectFile));
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.First().ProjectGuid.ToString().ShouldBe("646a532e-8943-5a4b-b106-e1341b4d3535");
        }

        [Test]
        public void BuildsProjectWithoutLogger([ValueSource(nameof(Preferences))] EnvironmentPreference preference)
        {
            // Given
            string projectFile = @"SdkNetCoreProject\SdkNetCoreProject.csproj";
            ProjectAnalyzer analyzer = new AnalyzerManager()
                .GetProject(GetProjectPath(projectFile));
            EnvironmentOptions options = new EnvironmentOptions
            {
                Preference = preference
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.Build(options);

            // Then
            results.Count.ShouldBeGreaterThan(0);
            results.OverallSuccess.ShouldBeTrue();
            results.ShouldAllBe(x => x.Succeeded);
        }

        private static ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log)
        {
            ProjectAnalyzer analyzer = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log
                })
                .GetProject(GetProjectPath(projectFile));

#pragma warning disable 0162
            if (BinaryLog)
            {
                analyzer.AddBinaryLogger(Path.Combine(@"E:\Temp\", Path.ChangeExtension(Path.GetFileName(projectFile), ".core.binlog")));
            }
#pragma warning restore 0162

            return analyzer;
        }

        private static string GetProjectPath(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(SimpleProjectsFixture).Assembly.Location),
                    @"..\..\..\..\projects\" + file));

            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        private static void DeleteProjectDirectory(string projectFile, string directory)
        {
            string path = Path.Combine(Path.GetDirectoryName(GetProjectPath(projectFile)), directory);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
