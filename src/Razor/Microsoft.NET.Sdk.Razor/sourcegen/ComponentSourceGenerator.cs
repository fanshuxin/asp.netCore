// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor
{
    [Generator]
    public partial class ComponentSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.ParseOptions is not CSharpParseOptions csharpParseOptions)
            {
                return;
            }

            var compilation = context.Compilation;

            var globalOptions = context.AnalyzerConfigOptions.GlobalOptions;

            if (!globalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace))
            {
                rootNamespace = "ASP";
            }

            if (!globalOptions.TryGetValue("build_property.RazorLangVersion", out var razorLanguageVersionString) ||
                !RazorLanguageVersion.TryParse(razorLanguageVersionString, out var razorLanguageVersion))
            {
                razorLanguageVersion = RazorLanguageVersion.Latest;
            }

            var razorConfiguration = RazorConfiguration.Create(razorLanguageVersion, "default", Enumerable.Empty<RazorExtension>());

            var tagHelperFeature = new StaticCompilationTagHelperFeature(() => compilation);

            var razorInputItems = GetRazorInputs(context);
            var fileSystem = GetVirtualFileSystem(razorInputItems);

            var configuration = RazorConfiguration.Default;
            var tagHelpers = GetTagHelperDescriptors();

            var projectEngine = RazorProjectEngine.Create(configuration, fileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.SetRootNamespace(rootNamespace);

                b.Features.Add(new StaticTagHelperFeature { TagHelpers = tagHelpers, });
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);

                b.SetCSharpLanguageVersion(csharpParseOptions.LanguageVersion);
            });

            var hintBuilder = new StringBuilder();

            foreach (var file in razorInputItems)
            {
                var codeGen = projectEngine.Process(projectEngine.FileSystem.GetItem(file.NormalizedPath, FileKinds.Component));

                GetIdentifierFromPath(hintBuilder, file.NormalizedPath);

                context.AddSource(hintBuilder.ToString(), SourceText.From(codeGen.GetCSharpDocument().GeneratedCode, Encoding.UTF8));
            }

            IReadOnlyList<TagHelperDescriptor> GetTagHelperDescriptors()
            {
                var discoveryProjectEngine = RazorProjectEngine.Create(configuration, fileSystem, b =>
                {
                    b.Features.Add(new DefaultTypeNameFeature());
                    b.Features.Add(new SetSuppressPrimaryMethodBodyOptionFeature());
                    b.Features.Add(new SuppressChecksumOptionsFeature());

                    b.SetRootNamespace(rootNamespace);

                    var metadataReferences = new List<MetadataReference>(context.Compilation.References);
                    b.Features.Add(new DefaultMetadataReferenceFeature { References = metadataReferences });

                    b.Features.Add(tagHelperFeature);
                    b.Features.Add(new DefaultTagHelperDescriptorProvider());

                    CompilerFeatures.Register(b);

                    b.SetCSharpLanguageVersion(csharpParseOptions.LanguageVersion);
                });

                foreach (var file in razorInputItems)
                {
                    var codeGen = discoveryProjectEngine.Process(discoveryProjectEngine.FileSystem.GetItem(file.NormalizedPath, FileKinds.Component));

                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(codeGen.GetCSharpDocument().GeneratedCode));
                }

                return tagHelperFeature.GetDescriptors();
            }
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

        private static List<RazorInputItem> GetRazorInputs(GeneratorExecutionContext context)
        {
            var razorItems = new List<RazorInputItem>();
            foreach (var item in context.AdditionalFiles.Where(f => f.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)))
            {
                var options = context.AnalyzerConfigOptions.GetOptions(item);
                if (!options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var relativePath))
                {
                    throw new InvalidOperationException($"Razor file '{item.Path}' does not have required value 'TargetPath'.");
                }

                options.TryGetValue("build_metadata.AdditionalFiles.CssScope", out var cssScope);

                razorItems.Add(new RazorInputItem(item.Path, relativePath, cssScope));
            }

            return razorItems;
        }

        private static void GetIdentifierFromPath(StringBuilder builder, string filePath)
        {
            builder.Length = 0;

            for (var i = 0; i < filePath.Length; i++)
            {
                builder.Append(filePath[i] switch
                {
                    ':' or '\\' or '/' => '_',
                    var @default => @default,
                });
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private class SetSuppressPrimaryMethodBodyOptionFeature : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
        {
            public int Order { get; set; }

            public void Configure(RazorCodeGenerationOptionsBuilder options)
            {
                options.SuppressPrimaryMethodBody = true;
            }
        }

        private class StaticTagHelperFeature : ITagHelperFeature
        {
            public RazorEngine Engine { get; set; }

            public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; set; }

            public IReadOnlyList<TagHelperDescriptor> GetDescriptors() => TagHelpers;
        }

        private readonly struct RazorInputItem
        {
            public RazorInputItem(string fullPath, string relativePath, string cssScope)
            {
                FullPath = fullPath;
                RelativePath = relativePath;
                CssScope = cssScope;
                NormalizedPath = '/' + relativePath
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace("//", "/");
            }

            public string FullPath { get; }

            public string RelativePath { get; }

            public string NormalizedPath { get; }

            public string CssScope { get; }
        }
    }
}
