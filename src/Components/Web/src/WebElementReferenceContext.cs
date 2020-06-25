using System;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components
{
    public class WebElementReferenceContext : ElementReferenceContext
    {
        internal IJSRuntime JSRuntime { get; }

        public WebElementReferenceContext(IJSRuntime jsRuntime)
        {
            JSRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
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
