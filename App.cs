using Elm327.Core;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Obd2Test
{
    public class App
    {
        //private ElmDriver _driver = null;
        private string _port = "";
        private string _server;
        private int _serverPort = 0;
        private bool _isRunning = false;
        private string _instance = "OBDII";
        private string _engineName;
        private Thread _thread;
        private Dictionary<string, string> _config;
        private int _noDataCounter = 0;

        public void Run(Dictionary<string, string> arguments)
        {
            _config = arguments;

            LoadConfiguration();

            _isRunning = true;
            
            _thread = new Thread(ReaderThread)
            {
                Name = "OBDI-Reader"
            };
            _thread.Start();

            _thread.Join();
            
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Log("App is exiting...");
                _isRunning = false;
            };
        }

        private void LoadConfiguration()
        {
            _port = GetConfig<string>("serial", null);
            _server = GetConfig("server", "localhost");
            _serverPort = GetConfig("server.port", 55557);
            _instance = GetConfig("name", _instance);
            _engineName = GetConfig("engine", "none");

            if (string.IsNullOrEmpty(_port))
            {
                throw new ArgumentException("-serial must be filled in!");
            }
        }

        private T GetConfig<T>(string key, T defaultValue = default(T))
        {
            if (_config.ContainsKey(key))
            {
                Log($"{key} = '{_config[key]}'");
                return (T)Convert.ChangeType(_config[key], typeof(T));
            }
            else
            {
                return defaultValue;
            }
        }

        private void ReaderThread(object obj)
        {
            Log($"Reader thread {_instance} started!");

            try
            {
                while (_isRunning)
                {
                    ReadData();
                    Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {
                Log("ReaderThread failure: " + ex.ToString());
            }

            Log("Reader thread exited!");
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void ReadData()
        {
            try
            {
                Log($"Opening port {_port}...");

                using (var driver = new ElmDriver(_port, ElmDriver.ElmObdProtocolType.Automatic, ElmDriver.ElmMeasuringUnitType.Metric))
                {
                    _noDataCounter = 0;
                    Log($"Connecting to OBDII...");

                    var result = driver.Connect();
                    driver.ProtocolType = ElmDriver.ElmObdProtocolType.Iso9141_2;

                    Log($"Connection result = {result}");

                    if (result == ElmDriver.ElmConnectionResultType.Connected)
                    {
                        /*for (int i = 0; i < 53; i++)
                        {
                            Console.WriteLine($"PID: {i} - {i.ToString("x")}");

                            var response = driver.ObdMode01.GetPidResponse(i.ToString("x"));
                            Console.WriteLine("*****************************");
                            Console.WriteLine(String.Join(Environment.NewLine, response));
                        }*/

                        while (_isRunning)
                        {
                            var rpm = driver.ObdMode01.EngineRpm;
                            var temp = driver.ObdMode01.EngineCoolantTemperature;
                            var massAirflow = driver.ObdMode01.MassAirFlowRate;

                            var line = $"{DateTime.Now.ToShortTimeString()}\tRPM: {rpm}, Temp: {temp} C, Mass airflow: {massAirflow} g/s, NoData: {_noDataCounter}";
                            Log(line);

                            if(rpm == 0)
                            {
                                _noDataCounter++;
                            }
                            else
                            {
                                _noDataCounter = 0;
                            }

                            if(_noDataCounter > 10)
                            {
                                break;
                            }

                            var signalKData = new Dictionary<string, object>
                            {
                                { $"propulsion.engine.{_engineName}.revolutions", Math.Round(rpm, 2) },
                                { $"propulsion.engine.{_engineName}.temperature", Math.Round(temp +  273.15)}
                            };

                            SendToSignalK(signalKData);

                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        Log($"Connection failed: {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Reader failed: {ex}");
            }
        }

        private void Log(object obj)
        {
            Console.WriteLine(obj?.ToString());
        }

        private void SendToSignalK(Dictionary<string, object> values)
        {
            var server = _server;

            
            var cmd = @"{""updates"": [{""$source"": ""SOURCE-HERE"",""values"":[".Replace("SOURCE-HERE", _instance);

            foreach (var item in values)
            {
                var valueString = "null";

                if (item.Value is string)
                {
                    valueString = $"\"{item.Value}\"";
                }
                else if (item.Value is double)
                {
                    valueString = item.Value.ToString();
                }

                cmd += @"{""path"":""" + item.Key + @""", ""value"": " + valueString + "},";
            }

            cmd = cmd.Substring(0, cmd.Length - 1);

            cmd += "]}]}";

            Log(cmd);
            
            using (var udp = new UdpClient(_server, _serverPort))
            {
                var data = Encoding.UTF8.GetBytes(cmd);
                udp.Send(data, data.Length);
            }
        }
    }
}
