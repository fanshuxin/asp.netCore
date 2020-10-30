// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RazorSourceGenerators;

namespace Microsoft.CodeAnalysis.Razor
{
    /// <summary>
    /// This source generator performs two operations
    /// * Discovers tag helpers
    /// * Code gens any component files and adds them to the compilation.
    /// </summary>
    [Generator]
    public partial class ComponentSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.ParseOptions is not CSharpParseOptions)
            {
                return;
            }

            var razorContext = RazorSourceGenerationContext.Create(context, ".razor");

            // TagHelpers are used by both components and views. Always discover them.
            var tagHelpers = ResolveTagHelperDescriptors(context, razorContext);

            if (razorContext.RazorFiles.Count == 0)
            {
                return;
            }

            CodeGenerateRazorComponents(context, razorContext, tagHelpers);
        }

        private static void CodeGenerateRazorComponents(GeneratorExecutionContext context, RazorSourceGenerationContext razorContext, IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            var projectEngine = RazorProjectEngine.Create(razorContext.Configuration, razorContext.FileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.SetRootNamespace(razorContext.RootNamespace);

                b.Features.Add(new StaticTagHelperFeature { TagHelpers = tagHelpers, });
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);

                b.SetCSharpLanguageVersion(((CSharpParseOptions)context.ParseOptions).LanguageVersion);
            });

            var files = razorContext.RazorFiles;
            var contextLock = new object();

            Parallel.For(0, files.Count, i =>
            {
                var file = files[i];

                var codeDocument = projectEngine.Process(projectEngine.FileSystem.GetItem(file.NormalizedPath, FileKinds.Component));
                var csharpDocument = codeDocument.GetCSharpDocument();
                for (var j = 0; j < csharpDocument.Diagnostics.Count; j++)
                {
                    var razorDiagnostic = csharpDocument.Diagnostics[j];
                    var csharpDiagnostic = razorDiagnostic.AsDiagnostic();
                    context.ReportDiagnostic(csharpDiagnostic);
                }

                var hint = GetIdentifierFromPath(file.NormalizedPath);

                lock (contextLock)
                {
                    context.AddSource(hint, SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode, Encoding.UTF8));
                }
            });
        }

        private static IReadOnlyList<TagHelperDescriptor> ResolveTagHelperDescriptors(GeneratorExecutionContext executionContext, RazorSourceGenerationContext razorContext)
        {
            var tagHelperFeature = new StaticCompilationTagHelperFeature();

            var discoveryProjectEngine = RazorProjectEngine.Create(razorContext.Configuration, razorContext.FileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.Features.Add(new SetSuppressPrimaryMethodBodyOptionFeature());
                b.Features.Add(new SuppressChecksumOptionsFeature());

                b.SetRootNamespace(razorContext.RootNamespace);

                var metadataReferences = new List<MetadataReference>(executionContext.Compilation.References);
                b.Features.Add(new DefaultMetadataReferenceFeature { References = metadataReferences });

                b.Features.Add(tagHelperFeature);
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);

                b.SetCSharpLanguageVersion(((CSharpParseOptions)executionContext.ParseOptions).LanguageVersion);
            });

            var files = razorContext.RazorFiles;
            var results = ArrayPool<SyntaxTree>.Shared.Rent(files.Count);
            var declarationFolder = Path.Combine(razorContext.IntermediateOutputPath, "RazorDeclaration");

            Parallel.For(0, files.Count, i =>
            {
                var file = files[i];
                var outputPath = Path.Combine(declarationFolder, file.RelativePath);
                if (File.GetLastWriteTimeUtc(outputPath) > File.GetLastWriteTimeUtc(file.FullPath))
                {
                    // Declaration files are invariant to other razor files, tag helpers, assemblies. If we have previously generated
                    // content that it's still newer than the output file, use it and save time processing the file.
                    using var outputFileStream = File.OpenRead(outputPath);
                    results[i] = CSharpSyntaxTree.ParseText(SourceText.From(outputFileStream));
                }
                else
                { 
                    var codeGen = discoveryProjectEngine.Process(discoveryProjectEngine.FileSystem.GetItem(file.NormalizedPath, FileKinds.Component));
                    var generatedCode = codeGen.GetCSharpDocument().GeneratedCode;
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllText(outputPath, generatedCode);
                    results[i] = CSharpSyntaxTree.ParseText(generatedCode);
                }
            });

            tagHelperFeature.Compilation = executionContext.Compilation.AddSyntaxTrees(results.Take(files.Count));
            ArrayPool<SyntaxTree>.Shared.Return(results);

            var lastUpdatedReferenceUtc = GetLastUpdatedReference(executionContext.Compilation.References);
            var tagHelperRefsOutputCache = Path.Combine(razorContext.IntermediateOutputPath, TagHelperSerializer.ReferenceAssemblyTagHelpersOutputPath);
            IReadOnlyList<TagHelperDescriptor> refTagHelpers;

            if (lastUpdatedReferenceUtc < File.GetLastWriteTimeUtc(tagHelperRefsOutputCache))
            {
                // Producing tag helpers from a Compilation every time is surprisingly expensive. So we'll use some caching strategies to mitigate this until
                // we can improve the perf in that area.

                // TagHelpers can come from two locations - the declaration files
                // and the assemblies participating in the compilation.
                // In a typical inner loop, the assemblies referenced by the project do not change. We could cache these separately from the tag helpers produced
                // by the app to avoid some per-compilation costs.
                // We determine if any of the reference assemblies have a newer timestamp than the output cache for the tag helpers. If not, we can re-use previously
                // calculated results.
                refTagHelpers = TagHelperSerializer.Deserialize(tagHelperRefsOutputCache);
            }
            else
            {
                tagHelperFeature.DiscoveryMode = TagHelperDiscoveryMode.References;
                refTagHelpers = tagHelperFeature.GetDescriptors();
                TagHelperSerializer.Serialize(tagHelperRefsOutputCache, refTagHelpers);
            }

            tagHelperFeature.DiscoveryMode = TagHelperDiscoveryMode.CurrentAssembly;
            var assemblyTagHelpers = tagHelperFeature.GetDescriptors();

            var tagHelperOutputCache = Path.Combine(razorContext.IntermediateOutputPath, TagHelperSerializer.CurrentAssemblyTagHelpersOutputPath);
            TagHelperSerializer.Serialize(tagHelperOutputCache, assemblyTagHelpers);

            var result = new List<TagHelperDescriptor>(refTagHelpers.Count + assemblyTagHelpers.Count);
            result.AddRange(assemblyTagHelpers);
            result.AddRange(refTagHelpers);

            return result;
        }

        private static DateTime GetLastUpdatedReference(IEnumerable<MetadataReference> references)
        {
            var lastWriteTimeUtc = DateTime.MinValue;

            foreach (var reference in references)
            {
                if (reference is not PortableExecutableReference portableExecutableReference || string.IsNullOrEmpty(portableExecutableReference.FilePath))
                {
                    continue;
                }

                var fileWriteTime = File.GetLastWriteTimeUtc(portableExecutableReference.FilePath);

                lastWriteTimeUtc = lastWriteTimeUtc < fileWriteTime ? fileWriteTime : lastWriteTimeUtc;
            }

            return lastWriteTimeUtc;
        }

        private static string GetIdentifierFromPath(string filePath)
        {
            var builder = new StringBuilder(filePath.Length);

            for (var i = 0; i < filePath.Length; i++)
            {
                builder.Append(filePath[i] switch
                {
                    ':' or '\\' or '/' => '_',
                    var @default => @default,
                });
            }

            return builder.ToString();
        }
    }
}
