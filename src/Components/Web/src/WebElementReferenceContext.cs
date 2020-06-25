using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components
{
    internal class WebElementReferenceContext
    {
        public IJSRuntime JSRuntime { get; }

        public WebElementReferenceContext(IJSRuntime jsRuntime)
        {
            JSRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }
    }

    internal class WebElementReferenceContextProvider : IElementReferenceContextProvider
    {
        private readonly IJSRuntime _jsRuntime;

        public WebElementReferenceContextProvider(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public object CreateElementReferenceContext()
            => new WebElementReferenceContext(_jsRuntime);
    }

    public static class WebElementReferenceContextServiceCollectionExtensions
    {
        public static void AddWebElementReferenceContext(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddScoped<IElementReferenceContextProvider, WebElementReferenceContextProvider>();
        }
    }

    internal static class ElementReferenceExtensions
    {
        public static IJSRuntime GetJSRuntime(this ElementReference elementReference)
        {
            var context = (WebElementReferenceContext)elementReference.Context;
            return context.JSRuntime;
        }
    }
}
