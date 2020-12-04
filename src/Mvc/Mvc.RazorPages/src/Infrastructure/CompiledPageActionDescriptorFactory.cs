// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    internal sealed class CompiledPageActionDescriptorFactory
    {
        private readonly IPageApplicationModelProvider[] _applicationModelProviders;
        private readonly ActionEndpointFactory _endpointFactory;
        private readonly PageConventionCollection _conventions;
        private readonly FilterCollection _globalFilters;

        public CompiledPageActionDescriptorFactory(
            IEnumerable<IPageApplicationModelProvider> applicationModelProviders,
            ActionEndpointFactory endpointFactory,
            IOptions<RazorPagesOptions> pageOptions,
            IOptions<MvcOptions> mvcOptions)
        {
            _applicationModelProviders = applicationModelProviders.OrderBy(a => a.Order).ToArray();
            _endpointFactory = endpointFactory;
            _conventions = pageOptions.Value.Conventions ?? throw new ArgumentNullException(nameof(RazorPagesOptions.Conventions));
            _globalFilters = mvcOptions.Value.Filters;
        }

        public CompiledPageActionDescriptor CreateCompiledDescriptor(
            PageActionDescriptor actionDescriptor,
            CompiledViewDescriptor viewDescriptor,
            EndpointMetadataCollection endpointMetadata)
        {
            var context = new PageApplicationModelProviderContext(actionDescriptor, viewDescriptor.Type.GetTypeInfo());
            for (var i = 0; i < _applicationModelProviders.Length; i++)
            {
                _applicationModelProviders[i].OnProvidersExecuting(context);
            }

            for (var i = _applicationModelProviders.Length - 1; i >= 0; i--)
            {
                _applicationModelProviders[i].OnProvidersExecuted(context);
            }

            ApplyConventions(_conventions, context.PageApplicationModel);

            var compiled = CompiledPageActionDescriptorBuilder.Build(context.PageApplicationModel, _globalFilters);
            actionDescriptor.CompiledPageDescriptor = compiled;

            var endpoints = new List<Endpoint>();
            _endpointFactory.AddEndpoints(
                endpoints,
                routeNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                action: compiled,
                routes: Array.Empty<ConventionalRouteEntry>(),
                conventions: new Action<EndpointBuilder>[]
                {
                    b =>
                    {
                        // Copy Endpoint metadata for PageActionActionDescriptor to the compiled one.
                        // This is particularly important for the runtime compiled scenario where endpoint metadata is added
                        // to the PageActionDescriptor, which needs to be accounted for when constructing the
                        // CompiledPageActionDescriptor as part of the one of the many matcher policies.
                        // Metadata from PageActionDescriptor is less significant than the one discovered from the compiled type.
                        // Consequently, we'll insert it at the beginning.
                        for (var i = endpointMetadata.Count - 1; i >=0; i--)
                        {
                            b.Metadata.Insert(0, endpointMetadata[i]);
                        }
                    },
                },
                createInertEndpoints: false);

            // In some test scenarios there's no route so the endpoint isn't created. This is fine because
            // it won't happen for real.
            compiled.Endpoint = endpoints.SingleOrDefault();

            return compiled;
        }

        internal static void ApplyConventions(
            PageConventionCollection conventions,
            PageApplicationModel pageApplicationModel)
        {
            var applicationModelConventions = GetConventions<IPageApplicationModelConvention>(pageApplicationModel.HandlerTypeAttributes);
            foreach (var convention in applicationModelConventions)
            {
                convention.Apply(pageApplicationModel);
            }

            var handlers = pageApplicationModel.HandlerMethods.ToArray();
            foreach (var handlerModel in handlers)
            {
                var handlerModelConventions = GetConventions<IPageHandlerModelConvention>(handlerModel.Attributes);
                foreach (var convention in handlerModelConventions)
                {
                    convention.Apply(handlerModel);
                }

                var parameterModels = handlerModel.Parameters.ToArray();
                foreach (var parameterModel in parameterModels)
                {
                    var parameterModelConventions = GetConventions<IParameterModelBaseConvention>(parameterModel.Attributes);
                    foreach (var convention in parameterModelConventions)
                    {
                        convention.Apply(parameterModel);
                    }
                }
            }

            var properties = pageApplicationModel.HandlerProperties.ToArray();
            foreach (var propertyModel in properties)
            {
                var propertyModelConventions = GetConventions<IParameterModelBaseConvention>(propertyModel.Attributes);
                foreach (var convention in propertyModelConventions)
                {
                    convention.Apply(propertyModel);
                }
            }

            IEnumerable<TConvention> GetConventions<TConvention>(
                IReadOnlyList<object> attributes)
            {
                return Enumerable.Concat(
                    conventions.OfType<TConvention>(),
                    attributes.OfType<TConvention>());
            }
        }
    }
}
