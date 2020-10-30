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

        public List<RazorInputItem> RazorFiles { get; private set; }

        public VirtualRazorProjectFileSystem FileSystem { get; private set; }

        public RazorConfiguration Configuration { get; private set; }

        public string IntermediateOutputPath { get; private set; }

        public static RazorSourceGenerationContext Create(GeneratorExecutionContext context, string fileExtension)
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
            var razorInputItems = GetRazorInputs(context, fileExtension);
            var fileSystem = GetVirtualFileSystem(razorInputItems);

            return new RazorSourceGenerationContext
            {
                RootNamespace = rootNamespace,
                Configuration = razorConfiguration,
                FileSystem = fileSystem,
                RazorFiles = razorInputItems,
                IntermediateOutputPath = intermediateOutputPath,
            };
        }

        private static VirtualRazorProjectFileSystem GetVirtualFileSystem(List<RazorInputItem> razorInputItems)
        {
            var fileSystem = new VirtualRazorProjectFileSystem();
            for (var i = 0; i < razorInputItems.Count; i++)
            {
                var item = razorInputItems[i];
                fileSystem.Add(new DefaultRazorProjectItem(
                    basePath: "/",
                    filePath: item.NormalizedPath,
                    relativePhysicalPath: item.RelativePath,
                    fileKind: FileKinds.Component,
                    file: new FileInfo(item.FullPath),
                    cssScope: item.CssScope));
            }

            return fileSystem;
        }

        private static List<RazorInputItem> GetRazorInputs(GeneratorExecutionContext context, string fileExtension)
        {
            var isComponent = fileExtension == ".razor";

            var razorItems = new List<RazorInputItem>();
            foreach (var item in context.AdditionalFiles.Where(f => f.Path.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase)))
            {
                var options = context.AnalyzerConfigOptions.GetOptions(item);
                if (!options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var relativePath))
                {
                    throw new InvalidOperationException($"Razor file '{item.Path}' does not have required value 'TargetPath'.");
                }

                var fileKind = isComponent ? FileKinds.GetComponentFileKindFromFilePath(item.Path) : FileKinds.Legacy;

                razorItems.Add(new RazorInputItem(item.Path, relativePath, fileKind));
            }

            return razorItems;
        }

    }
}
