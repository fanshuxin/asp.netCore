// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    internal class DefaultPageLoader : PageLoader
    {
        private readonly IViewCompilerProvider _viewCompilerProvider;
        private readonly CompiledPageActionDescriptorFactory _compiledPageActionDescriptorFactory;

        public DefaultPageLoader(
            IViewCompilerProvider viewCompilerProvider,
            CompiledPageActionDescriptorFactory compiledPageActionDescriptorFactory)
        {
            _viewCompilerProvider = viewCompilerProvider;
            _compiledPageActionDescriptorFactory = compiledPageActionDescriptorFactory;
        }

        private IViewCompiler Compiler => _viewCompilerProvider.GetCompiler();

        [Obsolete]
        public override Task<CompiledPageActionDescriptor> LoadAsync(PageActionDescriptor actionDescriptor)
            => LoadAsync(actionDescriptor, EndpointMetadataCollection.Empty);

        public override Task<CompiledPageActionDescriptor> LoadAsync(PageActionDescriptor actionDescriptor, EndpointMetadataCollection endpointMetadata)
        {
            if (actionDescriptor == null)
            {
                throw new ArgumentNullException(nameof(actionDescriptor));
            }

            var task = actionDescriptor.CompiledPageActionDescriptorTask;

            if (task != null)
            {
                return task;
            }

            return actionDescriptor.CompiledPageActionDescriptorTask = LoadAsyncCore(actionDescriptor, endpointMetadata);
        }

        private async Task<CompiledPageActionDescriptor> LoadAsyncCore(PageActionDescriptor actionDescriptor, EndpointMetadataCollection endpointMetadata)
        {
            var viewDescriptor = await Compiler.CompileAsync(actionDescriptor.RelativePath);
            var compiled = _compiledPageActionDescriptorFactory.CreateCompiledDescriptor(actionDescriptor, viewDescriptor, endpointMetadata);

            return compiled;
        }
    }
}
