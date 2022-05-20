using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SpaServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace $safeprojectname$
{
    internal enum VueServeMode
    {
        /// <summary>
        /// Indicates that the output of npm run serve should be redirected so that it can be parsed to detect when the server has initialized.<para/>
        /// Selecting this option may cause issues with Hot Reload when stopping and restarting the debugging session.
        /// </summary>
        Parse,

        /// <summary>
        /// Indicates that npm run serve should be opened in the background and the specified launch port should be polled to detect when the server has initialized.<para/>
        /// If this website is debugged using Visual Studio Code, this option may cause issues with Hot Reload when stopping and restarting the debugging session
        /// due to npm run serve running in the integrated Terminal rather than in the background.
        /// </summary>
        Poll,

        /// <summary>
        /// Indicates that npm run serve should be launched in a new window that will appear in your taskbar and that the specified launch port should be polled to detect when the server has initialized.<para/>
        /// This option can only be used on Microsoft Windows. If you are not on Microsoft Windows, to achieve this option you will need to run npm run serve before attempting
        /// to debug this application. If this argument is specified on a non-Windows system, this value is equivalent to <see cref="VueServeMode.Poll"/>.
        /// </summary>
        Isolated
    }

    internal static class VueDevelopmentServerMiddlewareExtensions
    {
        private static int Port { get; } = 8080;
        private static Uri DevelopmentServerEndpoint { get; } = new Uri($"http://127.0.0.1:{Port}");
        private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(60);

        private static string DoneMessage { get; } = "running at";

        public static void UseVueDevelopmentServer(this ISpaBuilder spa, VueServeMode serveMode = VueServeMode.Poll)
        {
            spa.UseProxyToSpaDevelopmentServer(async () =>
            {
                var loggerFactory = spa.ApplicationBuilder.ApplicationServices.GetService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Vue");

                if (IsRunning())
                {
                    return DevelopmentServerEndpoint;
                }

                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                var argsPrefix = string.Empty;

                if (isWindows)
                {
                    if (serveMode == VueServeMode.Isolated)
                        argsPrefix = "/c start /MIN npm ";
                    else
                        argsPrefix = "/c npm ";
                }

                bool shouldRedirect = serveMode == VueServeMode.Parse;

                var processInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd" : "npm",
                    Arguments = $"{argsPrefix}run serve",
                    WorkingDirectory = "ClientApp",
                    RedirectStandardError = shouldRedirect,
                    RedirectStandardInput = shouldRedirect,
                    RedirectStandardOutput = shouldRedirect,
                    UseShellExecute = false,
                };
                var process = Process.Start(processInfo);
                var tcs = new TaskCompletionSource<int>();

                if (shouldRedirect)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            string line;
                            while ((line = process.StandardOutput.ReadLine()) != null)
                            {
                                logger.LogInformation(line);
                                if (!tcs.Task.IsCompleted && line.Contains(DoneMessage))
                                {
                                    tcs.SetResult(1);
                                }
                            }
                        }
                        catch (EndOfStreamException ex)
                        {
                            logger.LogError(ex.ToString());
                            tcs.SetException(new InvalidOperationException("'npm run serve' failed.", ex));
                        }
                    });

                    _ = Task.Run(() =>
                    {
                        try
                        {
                            string line;
                            while ((line = process.StandardError.ReadLine()) != null)
                            {
                                logger.LogError(line);
                            }
                        }
                        catch (EndOfStreamException ex)
                        {
                            logger.LogError(ex.ToString());
                            tcs.SetException(new InvalidOperationException("'npm run serve' failed.", ex));
                        }
                    });
                }
                else
                {
                    _ = Task.Run(() =>
                    {
                        while (!IsRunning())
                        {
                            Task.Delay(50);
                        }

                        tcs.SetResult(1);
                    });
                }

                var timeout = Task.Delay(Timeout);
                if (await Task.WhenAny(timeout, tcs.Task) == timeout)
                {
                    throw new TimeoutException();
                }

                return DevelopmentServerEndpoint;
            });

        }

        private static bool IsRunning() => IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(x => x.Port)
            .Contains(Port);
    }
}