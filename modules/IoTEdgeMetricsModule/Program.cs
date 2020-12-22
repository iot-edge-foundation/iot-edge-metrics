namespace IoTEdgeMetricsModule
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System.Text.RegularExpressions;


    class Program
    {
        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");


            var thread = new Thread(() => ThreadBody(ioTHubModuleClient));
            thread.Start();
        }

        private static async void ThreadBody(object userContext)
        {
            var client = userContext as ModuleClient;

            if (client == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            System.Console.WriteLine("Loop started");

            while (true)
            {
                try
                {
                    // System.Console.WriteLine("Loop execution EDGEHUB START");
                    // System.Console.WriteLine("============================");

                    using var httpClientEH = new HttpClient();
                     
                    var urlEH = "http://edgeHub:9600/metrics"; 

                    var reqEH = new HttpRequestMessage(HttpMethod.Get, urlEH);

                    using var resEH = await httpClientEH.SendAsync(reqEH);

                    var jsonStringEH = await resEH.Content.ReadAsStringAsync();

                    // System.Console.WriteLine($"Response: {jsonStringEH}");
                    // System.Console.WriteLine("==========================");
                    // System.Console.WriteLine("Loop execution EDGEHUB END");

                    Console.WriteLine();

                    // System.Console.WriteLine("Loop execution EDGEAGENT START");
                    // System.Console.WriteLine("==============================");

                    using var httpClientEA = new HttpClient();
                     
                    var urlEA = "http://edgeAgent:9600/metrics"; 

                    var reqEA = new HttpRequestMessage(HttpMethod.Get, urlEA);

                    using var resEA = await httpClientEA.SendAsync(reqEA);

                    var jsonStringEA = await resEA.Content.ReadAsStringAsync();

                    // System.Console.WriteLine($"Response: {jsonStringEA}");
                    // System.Console.WriteLine("============================");
                    // System.Console.WriteLine("Loop execution EDGEAGENT END");

                    ShowMetrics(jsonStringEH, jsonStringEA);
                }
                catch (System.Exception ex)
                {  
                    System.Console.WriteLine($"Exception: {ex.Message}");
                }

                await Task.Delay(10000);
            }
        }

        public static void ShowMetrics(string inputEdgeHub, string inputEdgeAgent)
        {
            string patternReceivedTotal = @"edgehub_messages_received_total{[\.a-zA-Z""=_,0-9\/-]*id=""([\.a-zA-Z0-9\/]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";
            
            RegexOptions options = RegexOptions.Multiline;

            foreach (Match m in Regex.Matches(inputEdgeHub, patternReceivedTotal, options))
            {
                Console.WriteLine($"edgehub_messages_received_total from module '{m.Groups[1]}': {m.Groups[2]}");
            }

            string patternSentTotalPerModule = @"edgehub_messages_sent_total{[\.a-zA-Z""=_,0-9\/-]*,from=""([a-zA-Z0-9\/]*)"",to=""upstream""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

            foreach (Match m in Regex.Matches(inputEdgeHub, patternSentTotalPerModule, options))
            {
                Console.WriteLine($"edgehub_messages_sent_total to UPSTREAM: '{m.Groups[1]}': {m.Groups[2]}");
            }


            // Disk size, door 1024 delen om zelfde waarde te krijgen
            
            string patternDiskSizeLeft = @"edgeAgent_available_disk_space_bytes{[\.a-zA-Z""=_,0-9\/-]*disk_name=""([a-z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

            foreach (Match m in Regex.Matches(inputEdgeAgent, patternDiskSizeLeft, options))
            {
                var size = Convert.ToDouble(m.Groups[2].Value) / 1024;

                Console.WriteLine($"edgeAgent_available_disk_space_bytes - Disk size left for Disk: '{m.Groups[1]}': {size}");
            }

            string patternDiskSizeTotal = @"edgeAgent_total_disk_space_bytes{[\.a-zA-Z""=_,0-9\/-]*disk_name=""([a-z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

            foreach (Match m in Regex.Matches(inputEdgeAgent, patternDiskSizeTotal, options))
            {
                var size = Convert.ToDouble(m.Groups[2].Value) / 1024;

                Console.WriteLine($"edgeAgent_total_disk_space_bytes - total Disk size for Disk: '{m.Groups[1]}': {size}");
            }

            string patternUptime = @"edgeAgent_iotedged_uptime_seconds{[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

            foreach (Match m in Regex.Matches(inputEdgeAgent, patternUptime, options))
            {
                Console.WriteLine($"edgeAgent_iotedged_uptime_seconds: {m.Groups[1]} - {Convert.ToInt32(m.Groups[1].Value) / 60} MINUTES");
            }

            string patternCpuQuantile0dot99 = @"edgeAgent_used_cpu_percent{[\.a-zA-Z""=_,0-9\/-]*module_name=""([a-zA-Z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*quantile=""0.99""} ([\.\d]*)";

            foreach (Match m in Regex.Matches(inputEdgeAgent, patternCpuQuantile0dot99, options))
            {
                var perc = Convert.ToDouble(m.Groups[2].Value);
                Console.WriteLine($"edgeAgent_used_cpu_percent - CPU USAGE for module: '{m.Groups[1]}': {perc:N2} (Quantile 0.99)");
            }

        }
    }
}
