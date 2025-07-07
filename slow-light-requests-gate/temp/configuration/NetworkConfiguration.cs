
using application.interfaces.networking;
using infrastructure.networking;

namespace infrastructure.configuration
{
    public static class NetworkConfiguration 
    {
        public static IServiceCollection AddNetworkServices(this IServiceCollection services)
        {
            services.AddSingleton<INetworkServer, TcpNetworkServer>();
            services.AddSingleton<INetworkServer, UdpNetworkServer>();
            services.AddSingleton<INetworkServer, WebSocketNetworkServer>();
            
            services.AddSingleton<INetworkClient, TcpNetworkClient>();
            services.AddSingleton<INetworkClient, UdpNetworkClient>();
            services.AddSingleton<INetworkClient, WebSocketNetworkClient>();
            
            services.AddSingleton<NetworkServerManager>();
            services.AddSingleton<NetworkClientManager>();
            
            return services;
        }
    }
}
