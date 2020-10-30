// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using RazorSourceGenerators;

namespace Microsoft.CodeAnalysis.Razor
{
    [Generator]
    public partial class RazorViewSourceGenerator : ISourceGenerator
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

            var razorContext = RazorSourceGenerationContext.Create(context, "*.cshtml");
            if (razorContext.RazorFiles.Count == 0)
            {
                return;
            }

            var tagHelpers = GetTagHelpers(razorContext.IntermediateOutputPath);
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
                RazorExtensions.Register(b);

                b.SetCSharpLanguageVersion(((CSharpParseOptions)context.ParseOptions).LanguageVersion);
            });

            var hintBuilder = new StringBuilder();

            foreach (var file in razorContext.RazorFiles)
            {
                var codeDocument = projectEngine.Process(projectEngine.FileSystem.GetItem(file.NormalizedPath, FileKinds.Legacy));
                var csharpDocument = codeDocument.GetCSharpDocument();
                for (var i = 0; i < csharpDocument.Diagnostics.Count; i++)
                {
                    var razorDiagnostic = csharpDocument.Diagnostics[i];
                    var csharpDiagnostic = razorDiagnostic.AsDiagnostic();
                    context.ReportDiagnostic(csharpDiagnostic);
                }

                var hint = GetIdentifierFromPath(hintBuilder, file.NormalizedPath);
                context.AddSource(hintBuilder.ToString(), SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode, Encoding.UTF8));
            }
        }

        private static IReadOnlyList<TagHelperDescriptor> GetTagHelpers(string intermediateOutputPath)
        {
            var refAssemblyTagHelperOutputPath = Path.Combine(intermediateOutputPath, TagHelperSerializer.ReferenceAssemblyTagHelpersOutputPath);
            var currentAssemblyTagHelperOutputPath = Path.Combine(intermediateOutputPath, TagHelperSerializer.CurrentAssemblyTagHelpersOutputPath);

            return Enumerable.Concat(
                TagHelperSerializer.Deserialize(refAssemblyTagHelperOutputPath),
                TagHelperSerializer.Deserialize(currentAssemblyTagHelperOutputPath)).ToList();
        }

        private static string GetIdentifierFromPath(StringBuilder builder, string filePath)
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

            return builder.ToString();
        }
    }
}
