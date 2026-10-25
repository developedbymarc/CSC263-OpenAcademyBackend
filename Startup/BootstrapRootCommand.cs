// Startup/BootstrapRootCommand.cs
using System.Text;

namespace OpenAcademy.Startup;

internal static class BootstrapRootCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        // ReadKey throws when stdin isn't a terminal, so check up front and say why
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine(
                "bootstrap-root needs an interactive terminal (stdin + TTY). " +
                "Run: podman compose run --rm api bootstrap-root");
            return 2;
        }

        var bootstrapper = services.GetRequiredService<RootUserBootstrapper>();

        if (await bootstrapper.RootExistsAsync())
        {
            Console.WriteLine("A root user already exists. Nothing to do.");
            return 0;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var email = Line("Root email");
            var userName = Line("Root username");
            var password = Secret("Root password");

            if (password != Secret("Confirm password"))
            {
                Console.Error.WriteLine("Passwords do not match.");
                continue;
            }

            var result = await bootstrapper.TryCreateRootAsync(new RootCredentials(email, userName, password));

            switch (result.Outcome)
            {
                case RootCreateOutcome.Created:
                    Console.WriteLine("Root user created.");
                    return 0;
                case RootCreateOutcome.AlreadyExists:
                    Console.WriteLine("A root user was created in the meantime. Nothing to do.");
                    return 0;
                default:
                    foreach (var error in result.Errors) Console.Error.WriteLine($"- {error}");
                    break;
            }
        }

        Console.Error.WriteLine("Giving up after 3 attempts.");
        return 1;
    }

    private static string Line(string label)
    {
        Console.Write($"{label}: ");
        return (Console.ReadLine() ?? string.Empty).Trim();
    }

    private static string Secret(string label)
    {
        Console.Write($"{label}: ");
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);   // intercept = don't echo
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                {
                    Console.WriteLine();
                    return sb.ToString();
                }
                case ConsoleKey.Backspace:
                {
                    if (sb.Length > 0) sb.Length--;
                    break;
                }
                default:
                {
                    if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
                    break;
                }
            }
        }
    }
}