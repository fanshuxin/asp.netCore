using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components
{
    public static class TimingRegionExtensions
    {
        public static async Task LogAsync(this TimingRegion region, IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("timingRegion.logFromDotNet", region);
        }
    }
}
