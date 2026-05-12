﻿using Microsoft.AspNetCore.Http;

namespace Zonit.Extensions.Projects.Middlewares;

internal class ProjectMiddleware(RequestDelegate _next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ICatalogManager Workspace
    )
    {
        if (Workspace.State is null)
            await Workspace.InitializeAsync();

        await _next(httpContext);
    }
}