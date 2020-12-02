// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FormatterWebSite
{
    public class StartupWithComplexParentValidation
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers(options => options.ValidateComplexTypesIfChildValidationFails = true)
                .AddNewtonsoftJson(options => options.SerializerSettings.Converters.Insert(0, new IModelConverter()))
#pragma warning disable CS0618
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
#pragma warning restore CS0618
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
