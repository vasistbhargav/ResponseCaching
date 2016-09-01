﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.ResponseCaching
{
    public interface IResponseCachingCacheKeyModifier
    {
        /// <summary>
        /// Create a key segment that is prepended to the default cache key.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/>.</param>
        /// <returns>The key segment that will be prepended to the default cache key.</returns>
        string CreatKeyPrefix(HttpContext httpContext);

        /// <summary>
        /// Create a key segment that is appended to the default cache key.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/>.</param>
        /// <returns>The key segment that will be appended to the default cache key.</returns>
        string CreateKeySuffix(HttpContext httpContext);
    }
}
