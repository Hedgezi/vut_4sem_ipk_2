using CommandLine;

namespace vut_ipk2;

public class CommandLineOptions
{
    [Option('s', Default = "0.0.0.0", HelpText = "Server hostname.")]
    public string ServerHostname { get; set; }
    
    [Option('p', Default = 4567, HelpText = "Server port.")]
    public int ServerPort { get; set; }
    
    [Option('d', Default = 250, HelpText = "Timeout in milliseconds.")]
    public int Timeout { get; set; }
    
    [Option('r', Default = 3, HelpText = "Maximum number of UDP retransmissions.")]
    public int Retransmissions { get; set; }
}