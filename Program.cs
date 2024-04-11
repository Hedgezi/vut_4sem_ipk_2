using System.Net;
using CommandLine;
using vut_ipk2.Common.Auth;
using vut_ipk2.UdpServer;

namespace vut_ipk2;

class Program
{
    private static async Task Main(string[] args)
    {
        var options = new CommandLineOptions();
        Parser.Default.ParseArguments<CommandLineOptions>(args)
            .WithParsed(o => options = o);
        
        var authChecker = new AuthDataChecker();

        var udpServer = new UdpMainServer(
            IPAddress.Parse(options.ServerHostname),
            options.ServerPort,
            options.Timeout,
            options.Retransmissions,
            authChecker
        );
        
        var udpServerMainLoopTask = udpServer.AcceptNewUserLoopAsync();

        await Task.WhenAny(udpServerMainLoopTask);
    }
}