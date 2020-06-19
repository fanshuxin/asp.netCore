using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Components
{
    public static class TimingRegion
    {
        public static TimingRegionImpl Impl;
    }

    public abstract class TimingRegionImpl
    {
        public abstract int Open(string name);
        public abstract void Close(int id);
    }
}
