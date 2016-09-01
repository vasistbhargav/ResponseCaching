﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Microsoft.AspNetCore.ResponseCaching.Tests
{
    public class ResponseCachingTests
    {
        [Fact]
        public async void ServesCachedContent_IfAvailable()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfNotAvailable()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("/different");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByHeader_Matches()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.Response.Headers[HeaderNames.Vary] = HeaderNames.From;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                client.DefaultRequestHeaders.From = "user@example.com";
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfVaryByHeader_Mismatches()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.Response.Headers[HeaderNames.Vary] = HeaderNames.From;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                client.DefaultRequestHeaders.From = "user@example.com";
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.From = "user2@example.com";
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByParams_Matches()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = "param";
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?param=value");
                var subsequentResponse = await client.GetAsync("?param=value");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByParamsExplicit_Matches_ParamNameCaseInsensitive()
        {
            var builder = CreateBuilderWithResponseCaching(
            app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Features.Set<IHttpSendFileFeature>(new DummySendFileFeature());
                    await next.Invoke();
                });
            },
            async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "ParamA", "paramb" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?parama=valuea&paramb=valueb");
                var subsequentResponse = await client.GetAsync("?ParamA=valuea&ParamB=valueb");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByParamsStar_Matches_ParamNameCaseInsensitive()
        {
            var builder = CreateBuilderWithResponseCaching(
            app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Features.Set<IHttpSendFileFeature>(new DummySendFileFeature());
                    await next.Invoke();
                });
            },
            async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "*" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?parama=valuea&paramb=valueb");
                var subsequentResponse = await client.GetAsync("?ParamA=valuea&ParamB=valueb");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByParamsExplicit_Matches_OrderInsensitive()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "ParamB", "ParamA" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?ParamA=ValueA&ParamB=ValueB");
                var subsequentResponse = await client.GetAsync("?ParamB=ValueB&ParamA=ValueA");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfVaryByParamsStar_Matches_OrderInsensitive()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "*" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?ParamA=ValueA&ParamB=ValueB");
                var subsequentResponse = await client.GetAsync("?ParamB=ValueB&ParamA=ValueA");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfVaryByParams_Mismatches()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = "param";
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?param=value");
                var subsequentResponse = await client.GetAsync("?param=value2");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfVaryByParamsExplicit_Mismatch_ParamValueCaseSensitive()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "ParamA", "ParamB" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?parama=valuea&paramb=valueb");
                var subsequentResponse = await client.GetAsync("?parama=ValueA&paramb=ValueB");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfVaryByParamsStar_Mismatch_ParamValueCaseSensitive()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                context.GetResponseCachingFeature().VaryByParams = new[] { "*" };
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("?parama=valuea&paramb=valueb");
                var subsequentResponse = await client.GetAsync("?parama=ValueA&paramb=ValueB");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfRequestRequirements_NotMet()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    MaxAge = TimeSpan.FromSeconds(0)
                };
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void Serves504_IfOnlyIfCachedHeader_IsSpecified()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    OnlyIfCached = true
                };
                var subsequentResponse = await client.GetAsync("/different");

                initialResponse.EnsureSuccessStatusCode();
                Assert.Equal(System.Net.HttpStatusCode.GatewayTimeout, subsequentResponse.StatusCode);
            }
        }

        [Fact]
        public async void ServesCachedContent_WithoutSetCookie()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                headers.Headers[HeaderNames.SetCookie] = "cookieName=cookieValue";
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                initialResponse.EnsureSuccessStatusCode();
                subsequentResponse.EnsureSuccessStatusCode();

                foreach (var header in initialResponse.Headers)
                {
                    if (!string.Equals(HeaderNames.SetCookie, header.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(initialResponse.Headers.GetValues(header.Key), subsequentResponse.Headers.GetValues(header.Key));
                    }
                }
                Assert.True(initialResponse.Headers.Contains(HeaderNames.SetCookie));
                Assert.True(subsequentResponse.Headers.Contains(HeaderNames.Age));
                Assert.False(subsequentResponse.Headers.Contains(HeaderNames.SetCookie));
                Assert.Equal(await initialResponse.Content.ReadAsStringAsync(), await subsequentResponse.Content.ReadAsStringAsync());
            }
        }

        [Fact]
        public async void ServesCachedContent_IfIHttpSendFileFeature_NotUsed()
        {
            var builder = CreateBuilderWithResponseCaching(
                app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Features.Set<IHttpSendFileFeature>(new DummySendFileFeature());
                        await next.Invoke();
                    });
                },
                async (context) =>
                {
                    var uniqueId = Guid.NewGuid().ToString();
                    var headers = context.Response.GetTypedHeaders();
                    headers.CacheControl = new CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                    headers.Date = DateTimeOffset.UtcNow;
                    headers.Headers["X-Value"] = uniqueId;
                    await context.Response.WriteAsync(uniqueId);
                });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfIHttpSendFileFeature_Used()
        {
            var builder = CreateBuilderWithResponseCaching(
                app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Features.Set<IHttpSendFileFeature>(new DummySendFileFeature());
                        await next.Invoke();
                    });
                },
                async (context) =>
                {
                    var uniqueId = Guid.NewGuid().ToString();
                    var headers = context.Response.GetTypedHeaders();
                    headers.CacheControl = new CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                    headers.Date = DateTimeOffset.UtcNow;
                    headers.Headers["X-Value"] = uniqueId;
                    await context.Features.Get<IHttpSendFileFeature>().SendFileAsync("dummy", 0, 0, CancellationToken.None);
                    await context.Response.WriteAsync(uniqueId);
                });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfSubsequentRequest_ContainsNoStore()
        {
            var builder = CreateBuilderWithResponseCaching(
                async (context) =>
                {
                    var uniqueId = Guid.NewGuid().ToString();
                    var headers = context.Response.GetTypedHeaders();
                    headers.CacheControl = new CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                    headers.Date = DateTimeOffset.UtcNow;
                    headers.Headers["X-Value"] = uniqueId;
                    await context.Response.WriteAsync(uniqueId);
                });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    NoStore = true
                };
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfInitialRequestContains_NoStore()
        {
            var builder = CreateBuilderWithResponseCaching(
                async (context) =>
                {
                    var uniqueId = Guid.NewGuid().ToString();
                    var headers = context.Response.GetTypedHeaders();
                    headers.CacheControl = new CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                    headers.Date = DateTimeOffset.UtcNow;
                    headers.Headers["X-Value"] = uniqueId;
                    await context.Response.WriteAsync(uniqueId);
                });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                {
                    NoStore = true
                };
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void Serves304_IfIfModifiedSince_Satisfied()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.IfUnmodifiedSince = DateTimeOffset.MaxValue;
                var subsequentResponse = await client.GetAsync("");

                initialResponse.EnsureSuccessStatusCode();
                Assert.Equal(System.Net.HttpStatusCode.NotModified, subsequentResponse.StatusCode);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfIfModifiedSince_NotSatisfied()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.IfUnmodifiedSince = DateTimeOffset.MinValue;
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void Serves304_IfIfNoneMatch_Satisfied()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                headers.ETag = new EntityTagHeaderValue("\"E1\"");
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"E1\""));
                var subsequentResponse = await client.GetAsync("");

                initialResponse.EnsureSuccessStatusCode();
                Assert.Equal(System.Net.HttpStatusCode.NotModified, subsequentResponse.StatusCode);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfIfNoneMatch_NotSatisfied()
        {
            var builder = CreateBuilderWithResponseCaching(async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                headers.ETag = new EntityTagHeaderValue("\"E1\"");
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                client.DefaultRequestHeaders.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"E2\""));
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesCachedContent_IfBodySize_IsCacheable()
        {
            var builder = CreateBuilderWithResponseCaching(new ResponseCachingOptions()
            {
                MaximumCachedBodySize = 100
            },
            async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("");

                await AssertResponseCachedAsync(initialResponse, subsequentResponse);
            }
        }

        [Fact]
        public async void ServesFreshContent_IfBodySize_IsNotCacheable()
        {
            var builder = CreateBuilderWithResponseCaching(new ResponseCachingOptions()
            {
                MaximumCachedBodySize = 1
            },
            async (context) =>
            {
                var uniqueId = Guid.NewGuid().ToString();
                var headers = context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(10)
                };
                headers.Date = DateTimeOffset.UtcNow;
                headers.Headers["X-Value"] = uniqueId;
                await context.Response.WriteAsync(uniqueId);
            });

            using (var server = new TestServer(builder))
            {
                var client = server.CreateClient();
                var initialResponse = await client.GetAsync("");
                var subsequentResponse = await client.GetAsync("/different");

                await AssertResponseNotCachedAsync(initialResponse, subsequentResponse);
            }
        }

        private static async Task AssertResponseCachedAsync(HttpResponseMessage initialResponse, HttpResponseMessage subsequentResponse)
        {
            initialResponse.EnsureSuccessStatusCode();
            subsequentResponse.EnsureSuccessStatusCode();

            foreach (var header in initialResponse.Headers)
            {
                Assert.Equal(initialResponse.Headers.GetValues(header.Key), subsequentResponse.Headers.GetValues(header.Key));
            }
            Assert.True(subsequentResponse.Headers.Contains(HeaderNames.Age));
            Assert.Equal(await initialResponse.Content.ReadAsStringAsync(), await subsequentResponse.Content.ReadAsStringAsync());
        }

        private static async Task AssertResponseNotCachedAsync(HttpResponseMessage initialResponse, HttpResponseMessage subsequentResponse)
        {
            initialResponse.EnsureSuccessStatusCode();
            subsequentResponse.EnsureSuccessStatusCode();

            Assert.False(subsequentResponse.Headers.Contains(HeaderNames.Age));
            Assert.NotEqual(await initialResponse.Content.ReadAsStringAsync(), await subsequentResponse.Content.ReadAsStringAsync());
        }

        private static IWebHostBuilder CreateBuilderWithResponseCaching(RequestDelegate requestDelegate) =>
            CreateBuilderWithResponseCaching(app => { }, new ResponseCachingOptions(), requestDelegate);

        private static IWebHostBuilder CreateBuilderWithResponseCaching(Action<IApplicationBuilder> configureDelegate, RequestDelegate requestDelegate) =>
            CreateBuilderWithResponseCaching(configureDelegate, new ResponseCachingOptions(), requestDelegate);

        private static IWebHostBuilder CreateBuilderWithResponseCaching(ResponseCachingOptions options, RequestDelegate requestDelegate) =>
            CreateBuilderWithResponseCaching(app => { }, options, requestDelegate);

        private static IWebHostBuilder CreateBuilderWithResponseCaching(Action<IApplicationBuilder> configureDelegate, ResponseCachingOptions options, RequestDelegate requestDelegate)
        {
            return new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDistributedResponseCache();
                })
                .Configure(app =>
                {
                    configureDelegate(app);
                    app.UseResponseCaching(options);
                    app.Run(requestDelegate);
                });
        }

        private class DummySendFileFeature : IHttpSendFileFeature
        {
            public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellation)
            {
                return Task.FromResult(0);
            }
        }
    }
}
