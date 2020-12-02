// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Mvc.Razor.Compilation
{
    /// <summary>
    /// Represents a compiled Razor View or Page.
    /// </summary>
    public class CompiledViewDescriptor
    {
        /// <summary>
        /// Creates a new <see cref="CompiledViewDescriptor"/>.
        /// </summary>
        public CompiledViewDescriptor()
        {

        }

        /// <summary>
        /// Creates a new <see cref="CompiledViewDescriptor"/>.
        /// </summary>
        /// <param name="item">The <see cref="RazorCompiledItem"/>.</param>
        public CompiledViewDescriptor(RazorCompiledItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Item = item;
            ExpirationTokens = Array.Empty<IChangeToken>();
            RelativePath = ViewPath.NormalizePath(item.Identifier);
        }

        /// <summary>
        /// The normalized application relative path of the view.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// <see cref="IChangeToken"/> instances that indicate when this result has expired.
        /// </summary>
        public IList<IChangeToken> ExpirationTokens { get; set; }

        /// <summary>
        /// Gets the <see cref="RazorCompiledItem"/> descriptor for this view.
        /// </summary>
        public RazorCompiledItem Item { get; set; }

        /// <summary>
        /// Gets the type of the compiled item.
        /// </summary>
        public Type Type => Item?.Type;
    }
}