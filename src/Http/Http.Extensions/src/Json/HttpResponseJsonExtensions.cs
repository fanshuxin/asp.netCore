// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.AspNetCore.Http.Json
{
    public static class HttpResponseJsonExtensions
    {
        private static readonly string 

        private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        public static Task WriteJsonAsync(
            this HttpResponse response,
            object? value,
            Type type,
            JsonSerializerOptions? options = default,
            string? mediaType = default,
            Encoding? encoding = default,
            CancellationToken cancellationToken = default)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            options ??= (JsonSerializerOptions?)response.HttpContext.RequestServices.GetService(typeof(JsonSerializerOptions));
            options ??= DefaultSerializerOptions;

            // TODO handle charset

            mediaType ??= async"";


            return JsonSerializer.SerializeAsync(response.Body, value, type, options, cancellationToken);
        }

        public static Task WriteJsonAsync<TValue>(
            this HttpRequest response,
            TValue value,
            JsonSerializerOptions? options = default,
            string? mediaType = default,
            Encoding? encoding = default,
            CancellationToken cancellationToken = default)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            options ??= (JsonSerializerOptions?)response.HttpContext.RequestServices.GetService(typeof(JsonSerializerOptions));
            options ??= DefaultSerializerOptions;

            // TODO handle charset

            return JsonSerializer.SerializeAsync<TValue>(response.Body, value, options, cancellationToken);
        }
    }
}
