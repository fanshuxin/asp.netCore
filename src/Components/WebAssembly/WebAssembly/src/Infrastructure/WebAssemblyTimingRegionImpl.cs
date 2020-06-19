// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Microsoft.AspNetCore.Components.WebAssembly.Infrastructure
{
    internal class WebAssemblyTimingRegionImpl : TimingRegionImpl
    {
        public override int Open(string name)
        {
            return DefaultWebAssemblyJSRuntime.Instance.InvokeUnmarshalled<string, int>("timingRegion.open", name);
        }

        public override void Close(int id)
        {
            DefaultWebAssemblyJSRuntime.Instance.InvokeUnmarshalled<int, object>("timingRegion.close", id);
        }
    }
}
