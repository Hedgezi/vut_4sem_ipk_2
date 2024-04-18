using System.Net;
using CommandLine;
using vut_ipk2.TcpServer;
using vut_ipk2.UdpServer;

namespace vut_ipk2;

class Program
{
    private static async Task Main(string[] args)
    {
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

        await Task.WhenAny(udpServerMainLoopTask, tcpServerMainLoopTask);
    }
}