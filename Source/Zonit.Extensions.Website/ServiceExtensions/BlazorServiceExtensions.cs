//using Microsoft.Extensions.DependencyInjection;

//namespace Zonit.Libraries.Website.DependencyInjection;

//public static class BlazorServiceExtensions
//{
//    public static IServiceCollection BlazorServerSide(this IServiceCollection services)
//    {
//        services.AddServerSideBlazor(options =>
//        {
//            options.DetailedErrors = false;
//            //options.DisconnectedCircuitMaxRetained = 1000;
//            //options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromDays(7);
//            //options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
//            //options.MaxBufferedUnacknowledgedRenderBatches = 10;
//        }).AddCircuitOptions(o =>
//        {
//            //only add details when debugging
//            o.DetailedErrors = System.Diagnostics.Debugger.IsAttached;
//        }).AddHubOptions(options =>
//        {
//            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
//            options.EnableDetailedErrors = false;
//            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
//            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
//            options.MaximumParallelInvocationsPerClient = 1;
//            options.MaximumReceiveMessageSize = 4 * 128 * 1024;
//            options.StreamBufferCapacity = 10;
//        });

//        return services;
//    }

//    public static IServiceCollection BlazorWeb(this IServiceCollection services)
//    {
//        services
//            .AddRazorComponents()
//            .AddInteractiveServerComponents()
//            .AddHubOptions(options =>
//            {
//                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
//                options.EnableDetailedErrors = false;
//                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
//                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
//                options.MaximumParallelInvocationsPerClient = 1;
//                options.MaximumReceiveMessageSize = 4 * 128 * 1024;
//                options.StreamBufferCapacity = 10;
//            });

//        return services;
//    }

//    public static IServiceCollection BlazorWebAssembly(this IServiceCollection services)
//    {
//        services
//            .AddRazorComponents()
//            .AddInteractiveServerComponents()
//            .AddHubOptions(options =>
//            {
//                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
//                options.EnableDetailedErrors = false;
//                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
//                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
//                options.MaximumParallelInvocationsPerClient = 1;
//                options.MaximumReceiveMessageSize = 4 * 128 * 1024;
//                options.StreamBufferCapacity = 10;
//            })
//            .AddInteractiveWebAssemblyComponents();

//        return services;
//    }
//}