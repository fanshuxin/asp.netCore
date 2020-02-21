// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.AspNetCore.Http.Json
{
    public static class HttpRequestJsonExtensions
    {
        private static readonly JsonSerializerOptions DefaultSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        public static ValueTask<object?> ReadJsonAsync(
            this HttpRequest request,
            Type type,
            JsonSerializerOptions? options = default,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!request.HasFormContentType)
            {
                // TODO better error
                throw new InvalidOperationException();
            }

            options ??= (JsonSerializerOptions?)request.HttpContext.RequestServices.GetService(typeof(JsonSerializerOptions));
            options ??= DefaultSerializerOptions;

            // TODO handle charset

            return JsonSerializer.DeserializeAsync(request.Body, type, options, cancellationToken);
        }

        public static ValueTask<TValue> ReadJsonAsync<TValue>(
            this HttpRequest request,
            JsonSerializerOptions? options = default,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!request.HasFormContentType)
            {
                // TODO better error
                throw new InvalidOperationException();
            }

            options ??= (JsonSerializerOptions?)request.HttpContext.RequestServices.GetService(typeof(JsonSerializerOptions));
            options ??= DefaultSerializerOptions;

            // TODO handle charset

            return JsonSerializer.DeserializeAsync<TValue>(request.Body, options, cancellationToken);
        }
    }
}
