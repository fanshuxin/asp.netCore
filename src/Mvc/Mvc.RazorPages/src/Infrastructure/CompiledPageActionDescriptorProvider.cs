// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    /// <summary>
    /// A <see cref="IActionDescriptorProvider"/> for PageActions
    /// </summary>
    internal class CompiledPageActionDescriptorProvider : ICompiledPageActionDescriptorProvider
    {
        private readonly PageActionDescriptorProvider _pageActionDescriptorProvider;
        private readonly ApplicationPartManager _applicationPartManager;
        private readonly CompiledPageActionDescriptorFactory _compiledPageActionDescriptorFactory;

        public CompiledPageActionDescriptorProvider(
            IEnumerable<IPageRouteModelProvider> pageRouteModelProviders,
            ApplicationPartManager applicationPartManager,
            CompiledPageActionDescriptorFactory compiledPageActionDescriptorFactory,
            IOptions<MvcOptions> mvcOptionsAccessor,
            IOptions<RazorPagesOptions> pagesOptionsAccessor)
        {
            _pageActionDescriptorProvider = new PageActionDescriptorProvider(pageRouteModelProviders, mvcOptionsAccessor, pagesOptionsAccessor);
            _applicationPartManager = applicationPartManager;
            _compiledPageActionDescriptorFactory = compiledPageActionDescriptorFactory;
        }

        /// <inheritdoc/>
        public int Order => _pageActionDescriptorProvider.Order;

        /// <inheritdoc/>
        public void OnProvidersExecuting(ActionDescriptorProviderContext context)
        {
            var newContext = new ActionDescriptorProviderContext();
            _pageActionDescriptorProvider.OnProvidersExecuting(newContext);
            _pageActionDescriptorProvider.OnProvidersExecuted(newContext);

            var feature = new ViewsFeature();
            _applicationPartManager.PopulateFeature(feature);

            var lookup = new Dictionary<string, CompiledViewDescriptor>(feature.ViewDescriptors.Count, StringComparer.Ordinal);

            foreach (var viewDescriptor in feature.ViewDescriptors)
            {
                // View ordering has precedence semantics, a view with a higher precedence was not
                // already added to the list.
                lookup.TryAdd(ViewPath.NormalizePath(viewDescriptor.RelativePath), viewDescriptor);
            }

            foreach (var item in newContext.Results)
            {
                var pageActionDescriptor = (PageActionDescriptor)item;
                if (!lookup.TryGetValue(pageActionDescriptor.RelativePath, out var compiledViewDescriptor))
                {
                    throw new InvalidOperationException($"A descriptor for '{pageActionDescriptor.RelativePath}' was not found.");
                }

                var compiledPageActionDescriptor = _compiledPageActionDescriptorFactory.CreateCompiledDescriptor(
                    pageActionDescriptor,
                    compiledViewDescriptor,
                    EndpointMetadataCollection.Empty);
                context.Results.Add(compiledPageActionDescriptor);
            }
        }

        public void OnProvidersExecuted(ActionDescriptorProviderContext context)
        {
        }
    }
}
