using System.Net;
using CommandLine;
using vut_ipk2.Common.Interfaces;
using vut_ipk2.TcpServer;
using vut_ipk2.UdpServer;

namespace vut_ipk2;

class Program
{
    private static readonly HashSet<IMainServer> Servers = new();
    
    private static async Task Main(string[] args)
    {
        // Handle Ctrl+C by properly shutting down all servers
        // and sending all clients a BYE message
        Console.CancelKeyPress += async (_, e) =>
        {
            e.Cancel = true;
            
            foreach (var server in Servers)
                await server.PowerOffAsync();

            await Console.Error.WriteLineAsync("Exiting...");
            Environment.Exit(0);
        };
        
        var options = new CommandLineOptions();
        Parser.Default.ParseArguments<CommandLineOptions>(args)
            .WithParsed(o => options = o);
        
        var udpServer = new UdpMainServer(
            IPAddress.Parse(options.ServerHostname),
            options.ServerPort,
            options.Timeout,
            options.Retransmissions
        );
        
        var tcpServer = new TcpMainServer(
            IPAddress.Parse(options.ServerHostname),
            options.ServerPort
        );
        
        var udpServerMainLoopTask = udpServer.AcceptNewUserLoopAsync();
        var tcpServerMainLoopTask = tcpServer.AcceptNewUserLoopAsync();
        
        Servers.Add(udpServer);
        Servers.Add(tcpServer);

        await Task.WhenAll(udpServerMainLoopTask, tcpServerMainLoopTask);
    }
}