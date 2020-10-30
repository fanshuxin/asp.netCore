// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;

namespace RazorSourceGenerators
{
    internal static class RazorDiagnosticExtensions
    {
        public static Diagnostic AsDiagnostic(this RazorDiagnostic razorDiagnostic)
        {
            var descriptor = new DiagnosticDescriptor(
                razorDiagnostic.Id,
                razorDiagnostic.GetMessage(CultureInfo.CurrentCulture),
                razorDiagnostic.GetMessage(CultureInfo.CurrentCulture),
                "Razor",
                razorDiagnostic.Severity switch
                {
                    RazorDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                    RazorDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                    _ => DiagnosticSeverity.Hidden,
                },
                isEnabledByDefault: true);

            var span = razorDiagnostic.Span;
            var location = Location.Create(
                span.FilePath,
                 span.AsTextSpan(),
                 new LinePositionSpan(
                     new LinePosition(span.LineIndex, span.CharacterIndex),
                     new LinePosition(span.LineIndex, span.CharacterIndex + span.Length)));

            return Diagnostic.Create(descriptor, location);
        }
    }
}
