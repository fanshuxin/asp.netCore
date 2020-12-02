// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BasicWebSite
{
    public class StartupWithNewtonsoftJson
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
#pragma warning disable CS0618
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
#pragma warning restore CS0618
                .AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseEndpoints((endpoints) => endpoints.MapDefaultControllerRoute());
        }
    }
}
