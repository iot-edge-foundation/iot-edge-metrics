namespace IoTEdgeMetricsModule
{
    using System;
    using System.Net.Http;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    class Program
    {
        private static bool DefaultEdgeHubMetricsVisible = false;

        private static bool DefaultEdgeAgentMetricsVisible = false;

        private static bool DefaultEnableSendMessages = false;

        private static int DefaultInterval = 10000;

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

            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, ioTHubModuleClient);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

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
                    if (EdgeHubMetricsVisible)
                    {
                        System.Console.WriteLine("Loop execution EDGEHUB START");
                        System.Console.WriteLine("============================");
                    }

                    using var httpClientEH = new HttpClient();
                     
                    var urlEH = "http://edgeHub:9600/metrics"; 

                    var reqEH = new HttpRequestMessage(HttpMethod.Get, urlEH);

                    using var resEH = await httpClientEH.SendAsync(reqEH);

                    var jsonStringEH = await resEH.Content.ReadAsStringAsync();

                    if (EdgeHubMetricsVisible)
                    {
                        System.Console.WriteLine($"Response: {jsonStringEH}");
                        System.Console.WriteLine("==========================");
                        System.Console.WriteLine("Loop execution EDGEHUB END");
                        Console.WriteLine();
                    }

                    if (EdgeAgentMetricsVisible)
                    {
                        System.Console.WriteLine("Loop execution EDGEAGENT START");
                        System.Console.WriteLine("==============================");
                    }

                    using var httpClientEA = new HttpClient();
                     
                    var urlEA = "http://edgeAgent:9600/metrics"; 

                    var reqEA = new HttpRequestMessage(HttpMethod.Get, urlEA);

                    using var resEA = await httpClientEA.SendAsync(reqEA);

                    var jsonStringEA = await resEA.Content.ReadAsStringAsync();

                    if (EdgeAgentMetricsVisible)
                    {
                        System.Console.WriteLine($"Response: {jsonStringEA}");
                        System.Console.WriteLine("============================");
                        System.Console.WriteLine("Loop execution EDGEAGENT END");
                    }

                    var metrics = ConstructMetrics(jsonStringEH, jsonStringEA);

                    var jsonMessage = JsonConvert.SerializeObject(metrics);

                    if (EnableSendMessages)
                    {
                        System.Console.WriteLine($"Sent: {jsonMessage}");

                        using (var message = new Message(Encoding.UTF8.GetBytes(jsonMessage)))
                        { 
                            // Set message body type and content encoding for routing using decoded body values.
                            message.ContentEncoding = "utf-8";
                            message.ContentType = "application/json";

                            message.Properties.Add("messageType", "metrics");

                            try
                            {
                                await client.SendEventAsync("output1", message);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Exception '{ex.Message}' while sending output message '{jsonMessage}'");
                            }
                        }
                    }
                    else
                    {
                        System.Console.WriteLine($"Constructed: {jsonMessage}");
                    }

                }
                catch (System.Exception ex)
                {  
                    System.Console.WriteLine($"Exception: {ex.Message}");
                }

                await Task.Delay(Interval);
            }
        }

        public static Metrics ConstructMetrics(string inputEdgeHub, string inputEdgeAgent)
        {
            var deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");

            var metrics = new Metrics();

            RegexOptions options = RegexOptions.Multiline;

            try
            {
                //// Total received messages from modules by the edgehub 

                string patternReceivedTotal = @"edgehub_messages_received_total{[\.a-zA-Z""=_,0-9\/-]*id=""([\.a-zA-Z0-9\/]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";
                
                foreach (Match m in Regex.Matches(inputEdgeHub, patternReceivedTotal, options))
                {
                    var moduleName = m.Groups[1].Value;
                    moduleName = moduleName.Replace(deviceId, "");
                    moduleName = moduleName.Replace("/", "");

                    var count = Convert.ToInt32( m.Groups[2].Value);

                    Console.WriteLine($"edgehub_messages_received_total from module '{moduleName}': {count}");

                    // we add add modules which send messages to the edge hub
                    metrics.Modules.Add( moduleName, new Module{MessagesRecievedCount = count});
                }

                //// Total sent messages to the cloud by the edgehub

                string patternSentTotalPerModule = @"edgehub_messages_sent_total{[\.a-zA-Z""=_,0-9\/-]*,from=""([a-zA-Z0-9\/]*)"",to=""upstream""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

                foreach (Match m in Regex.Matches(inputEdgeHub, patternSentTotalPerModule, options))
                {
                    var moduleName = m.Groups[1].Value;
                    moduleName = moduleName.Replace(deviceId, "");
                    moduleName = moduleName.Replace("/", "");

                    var count = Convert.ToInt32( m.Groups[2].Value);

                    Console.WriteLine($"edgehub_messages_sent_total to UPSTREAM: '{m.Groups[1]}': {m.Groups[2]}");

                    if (metrics.Modules.ContainsKey(moduleName))
                    {
                        metrics.Modules[moduleName].MessagesSentCount = count;
                    } 
                    else
                    {
                        metrics.Modules.Add( moduleName, new Module{MessagesSentCount = count});
                    }
                }
            }
            catch (Exception exEh)
            {
                System.Console.WriteLine($"Exception found: {exEh.ToString()}");
                System.Console.WriteLine($"in:");
                System.Console.WriteLine(inputEdgeHub);
                System.Console.WriteLine();
            }

            try
            {
                //// Available disk size, devided by 1024 to get the same values as what 'linux df' is showing
                
                string patternDiskSizeLeft = @"edgeAgent_available_disk_space_bytes{[\.a-zA-Z""=_,0-9\/-]*disk_name=""([a-z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

                foreach (Match m in Regex.Matches(inputEdgeAgent, patternDiskSizeLeft, options))
                {
                    var size = Convert.ToDouble(m.Groups[2].Value) / 1024;

                    var diskName = m.Groups[1].Value;

                    Console.WriteLine($"edgeAgent_available_disk_space_bytes - Disk size left for Disk: '{diskName}': {size}");

                    // we only add disks
                    metrics.Disks.Add(diskName, new Disk{Free = size });
                }

                //// Total disk size

                string patternDiskSizeTotal = @"edgeAgent_total_disk_space_bytes{[\.a-zA-Z""=_,0-9\/-]*disk_name=""([a-z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

                foreach (Match m in Regex.Matches(inputEdgeAgent, patternDiskSizeTotal, options))
                {
                    var size = Convert.ToDouble(m.Groups[2].Value) / 1024;

                    var diskName = m.Groups[1].Value;

                    Console.WriteLine($"edgeAgent_total_disk_space_bytes - total Disk size for Disk: '{diskName}': {size}");

                    // we only append disks
                    if (metrics.Disks.ContainsKey(diskName))
                    {
                        var disk = metrics.Disks[diskName];
                        disk.Size = size;
                        disk.Used = disk.Size - disk.Free;
                    }
                }

                //// uptime of host (in minutes) (not the edgeAgent)

                string patternUptime = @"edgeAgent_iotedged_uptime_seconds{[\.a-zA-Z""=_,0-9\/-]*} (\d*)";

                foreach (Match m in Regex.Matches(inputEdgeAgent, patternUptime, options))
                {
                    var uptime = Convert.ToInt32(m.Groups[1].Value) / 60;
                    Console.WriteLine($"edgeAgent_iotedged_uptime_seconds: {m.Groups[1]} - {uptime} MINUTES");

                    metrics.Uptime = uptime;
                }

                //// CPU percentage used by each module

                string patternCpuQuantile0dot99 = @"edgeAgent_used_cpu_percent{[\.a-zA-Z""=_,0-9\/-]*module_name=""([a-zA-Z0-9]*)""[\.a-zA-Z""=_,0-9\/-]*quantile=""0.99""} ([\.\d]*)";

                foreach (Match m in Regex.Matches(inputEdgeAgent, patternCpuQuantile0dot99, options))
                {
                    var moduleName = m.Groups[1].Value; 
                    var perc = Convert.ToDouble(m.Groups[2].Value);
                    Console.WriteLine($"edgeAgent_used_cpu_percent - CPU USAGE for module: '{moduleName}': {perc:N2} (Quantile 0.99)");

                    // We add ALL modules
                    if (metrics.Modules.ContainsKey(moduleName))
                    {
                        metrics.Modules[moduleName].Cpu099 = Math.Round(perc, 2, MidpointRounding.AwayFromZero);
                    } 
                    else
                    {
                        metrics.Modules.Add(moduleName, new Module{Cpu099 = Math.Round(perc, 2, MidpointRounding.AwayFromZero)});
                    }
                }
            }
            catch (Exception exEa)
            {
                System.Console.WriteLine($"Exception found: {exEa.ToString()}");
                System.Console.WriteLine($"in:");
                System.Console.WriteLine(inputEdgeAgent);
                System.Console.WriteLine();
            }

            return metrics;
        }

        public static bool EdgeHubMetricsVisible { get; set; } = DefaultEdgeHubMetricsVisible;

        public static bool EdgeAgentMetricsVisible { get; set; } = DefaultEdgeAgentMetricsVisible;

        public static int Interval {get; set;} = DefaultInterval;

        public static bool EnableSendMessages {get; set;} = DefaultEnableSendMessages;

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException($"UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("interval")) 
                {
                    if (desiredProperties["interval"] != null)
                    {
                        Interval = Convert.ToUInt32(desiredProperties["interval"]);
                    }
                    else
                    {
                        Interval = DefaultInterval;
                    }

                    Console.WriteLine($"Interval changed to {Interval}");

                    reportedProperties["interval"] = Interval;
                }


                if (desiredProperties.Contains("enableSendMessages")) 
                {
                    if (desiredProperties["enableSendMessages"] != null)
                    {
                        var enableSendMessages = Convert.ToBoolean(desiredProperties["enableSendMessages"]);

                        EnableSendMessages = enableSendMessages;
                    }
                    else
                    {
                        EnableSendMessages = DefaultEnableSendMessages;
                    }

                    Console.WriteLine($"EnableSendMessages changed to {EnableSendMessages}");

                    reportedProperties["enableSendMessages"] = EnableSendMessages;
                }

                if (desiredProperties.Contains("edgeHubMetricsVisible")) 
                {
                    if (desiredProperties["edgeHubMetricsVisible"] != null)
                    {
                        var edgeHubMetricsVisible = Convert.ToBoolean(desiredProperties["edgeHubMetricsVisible"]);

                        EdgeHubMetricsVisible = edgeHubMetricsVisible;
                    }
                    else
                    {
                        EdgeHubMetricsVisible = DefaultEdgeHubMetricsVisible;
                    }

                    Console.WriteLine($"EdgeHubMetricsVisible changed to {EdgeHubMetricsVisible}");

                    reportedProperties["edgeHubMetricsVisible"] = EdgeHubMetricsVisible;
                }

                if (desiredProperties.Contains("edgeAgentMetricsVisible")) 
                {
                    if (desiredProperties["edgeAgentMetricsVisible"] != null)
                    {
                        var edgeAgentMetricsVisible = Convert.ToBoolean(desiredProperties["edgeAgentMetricsVisible"]);

                        EdgeAgentMetricsVisible = edgeAgentMetricsVisible;
                    }
                    else
                    {
                        EdgeAgentMetricsVisible = DefaultEdgeAgentMetricsVisible;
                    }

                    Console.WriteLine($"EdgeAgentMetricsVisible changed to {EdgeAgentMetricsVisible}");

                    reportedProperties["edgeAgentMetricsVisible"] = EdgeAgentMetricsVisible;
                }

                if (reportedProperties.Count > 0)
                {
                    client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
