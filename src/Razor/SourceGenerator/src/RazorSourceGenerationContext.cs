// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor
{
    internal sealed class RazorSourceGenerationContext
    {
        public string RootNamespace { get; private set; }

        public IReadOnlyList<RazorInputItem> RazorFiles { get; private set; }

        public IReadOnlyList<RazorInputItem> CshtmlFiles { get; private set; }

        public VirtualRazorProjectFileSystem FileSystem { get; private set; }

        public RazorConfiguration Configuration { get; private set; }

        public string IntermediateOutputPath { get; private set; }

        public static RazorSourceGenerationContext Create(GeneratorExecutionContext context)
        {
            var globalOptions = context.AnalyzerConfigOptions.GlobalOptions;

            if (!globalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace))
            {
                rootNamespace = "ASP";
            }

            globalOptions.TryGetValue("build_property._IntermediateOutputFullPath", out var intermediateOutputPath);

            if (!globalOptions.TryGetValue("build_property.RazorLangVersion", out var razorLanguageVersionString) ||
                !RazorLanguageVersion.TryParse(razorLanguageVersionString, out var razorLanguageVersion))
            {
                razorLanguageVersion = RazorLanguageVersion.Latest;
            }

            if (!globalOptions.TryGetValue("build_property.RazorConfiguration", out var configurationName))
            {
                configurationName = "default";
            }

            var razorConfiguration = RazorConfiguration.Create(razorLanguageVersion, configurationName, Enumerable.Empty<RazorExtension>());
            var (razorFiles, cshtmlFiles) = GetRazorInputs(context);
            var fileSystem = GetVirtualFileSystem(razorFiles, cshtmlFiles);

            return new RazorSourceGenerationContext
            {
                RootNamespace = rootNamespace,
                Configuration = razorConfiguration,
                FileSystem = fileSystem,
                RazorFiles = razorFiles,
                CshtmlFiles = cshtmlFiles,
                IntermediateOutputPath = intermediateOutputPath,
            };
        }

        private static VirtualRazorProjectFileSystem GetVirtualFileSystem(IReadOnlyList<RazorInputItem> razorFiles, IReadOnlyList<RazorInputItem> cshtmlFiles)
        {
            var fileSystem = new VirtualRazorProjectFileSystem();
            for (var i = 0; i < razorFiles.Count; i++)
            {
                var item = razorFiles[i];
                fileSystem.Add(new DefaultRazorProjectItem(
                    basePath: "/",
                    filePath: item.NormalizedPath,
                    relativePhysicalPath: item.RelativePath,
                    fileKind: FileKinds.Component,
                    file: new FileInfo(item.FullPath),
                    cssScope: item.CssScope));
            }

            for (var i = 0; i < cshtmlFiles.Count; i++)
            {
                var item = cshtmlFiles[i];
                fileSystem.Add(new DefaultRazorProjectItem(
                    basePath: "/",
                    filePath: item.NormalizedPath,
                    relativePhysicalPath: item.RelativePath,
                    fileKind: FileKinds.Legacy,
                    file: new FileInfo(item.FullPath),
                    cssScope: item.CssScope));
            }

            return fileSystem;
        }

        private static (IReadOnlyList<RazorInputItem> razorFiles, IReadOnlyList<RazorInputItem> cshtmlFiles) GetRazorInputs(GeneratorExecutionContext context)
        {
            List<RazorInputItem> razorFiles = null;
            List<RazorInputItem> cshtmlFiles = null;

            foreach (var item in context.AdditionalFiles)
            {
                var path = item.Path;
                var isComponent = path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
                var isRazorView = !isComponent && path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);

                if (!isComponent && !isRazorView)
                {
                    continue;
                }

                var options = context.AnalyzerConfigOptions.GetOptions(item);
                if (!options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var relativePath))
                {
                    throw new InvalidOperationException($"Razor file '{item.Path}' does not have required value 'TargetPath'.");
                }

                var fileKind = isComponent ? FileKinds.GetComponentFileKindFromFilePath(item.Path) : FileKinds.Legacy;

                if (isComponent)
                {
                    razorFiles ??= new();
                    razorFiles.Add(new RazorInputItem(item.Path, relativePath, fileKind));
                }
                else
                {
                    cshtmlFiles ??= new();
                    cshtmlFiles.Add(new RazorInputItem(item.Path, relativePath, fileKind));
                }
            }

            return (
                (IReadOnlyList<RazorInputItem>)razorFiles ?? Array.Empty<RazorInputItem>(),
                (IReadOnlyList<RazorInputItem>)cshtmlFiles ?? Array.Empty<RazorInputItem>()
            );
        }

    }
}
