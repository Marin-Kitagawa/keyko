using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Keyko.Services;

/// <summary>
/// Single-instance CLI companion. The running app hosts a named pipe;
/// `Keyko.exe run Google | list | pause | resume | profile Work | version`
/// sends a command and prints the response.
/// </summary>
public static class CliService
{
    public const string PipeName = "Keyko.Cli";

    public static bool IsCliCommand(string[] args) =>
        args.Length > 0 && args[0].ToLowerInvariant() is "run" or "list" or "pause" or "resume" or "profile" or "version" or "toggle";

    /// <summary>CLI side: connect to the running instance, send command, print response.</summary>
    public static int RunClient(string[] args)
    {
        var command = string.Join(' ', args);
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(command);
            var line = new StreamReader(pipe).ReadLine();
            Console.Out.WriteLine(line ?? "(no response)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Keyko isn't reachable — is it running? (" + ex.Message + ")");
            return 1;
        }
    }

    /// <summary>Server side: hosted by the running app on a background thread.</summary>
    public static void StartServer(Func<string, string> handle)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server) { AutoFlush = true };
                    var command = reader.ReadLine() ?? "";
                    var response = handle(command);
                    writer.WriteLine(response);
                }
                catch { /* keep serving */ }
            }
        })
        { IsBackground = true, Name = "Keyko.Cli" };
        t.Start();
    }
}
