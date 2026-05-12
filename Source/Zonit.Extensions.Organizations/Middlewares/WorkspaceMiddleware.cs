﻿using Microsoft.AspNetCore.Http;

namespace Zonit.Extensions.Organizations.Middlewares;

internal class WorkspaceMiddleware(RequestDelegate _next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IWorkspaceManager Workspace
    )
    {
        if(Workspace.State is null)
            await Workspace.InitializeAsync();

        await _next(httpContext);
    }
}