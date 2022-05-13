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
    public static class VueDevelopmentServerMiddlewareExtensions
    {
        private static int Port { get; } = 8080;
        private static Uri DevelopmentServerEndpoint { get; } = new Uri($"http://127.0.0.1:{Port}");
        private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(60);

        private static string DoneMessage { get; } = "running at";

        public static void UseVueDevelopmentServer(this ISpaBuilder spa, bool redirectServerOutput = false)
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
                var processInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd" : "npm",
                    Arguments = $"{(isWindows ? "/c npm " : "")}run serve",
                    WorkingDirectory = "ClientApp",
                    RedirectStandardError = redirectServerOutput,
                    RedirectStandardInput = redirectServerOutput,
                    RedirectStandardOutput = redirectServerOutput,
                    UseShellExecute = false,
                };
                var process = Process.Start(processInfo);
                var tcs = new TaskCompletionSource<int>();

                if (redirectServerOutput)
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