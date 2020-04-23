using Elm327.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private string _logFile;
        private int _noDataTimeout;
        private Thread _thread;
        private Dictionary<string, string> _config;
        private DateTime? _lastData = null;
        private int _readInterval = 1000;

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
            _logFile = GetConfig<string>("log", null);
            _noDataTimeout = GetConfig("datatimeout", 10000);
            _readInterval = GetConfig("readinterval", _readInterval);

            if (string.IsNullOrEmpty(_port))
            {
                throw new ArgumentException("-serial parameter must be filled in!");
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
                    _lastData = null;
                    Log($"Connecting to OBDII...");

                    var result = driver.Connect();
                    driver.ProtocolType = ElmDriver.ElmObdProtocolType.Iso9141_2;

                    Log($"Connection result = {result}");

                    if (result == ElmDriver.ElmConnectionResultType.Connected)
                    {
                        _lastData = DateTime.UtcNow;
                        /*for (int i = 0; i < 53; i++)
                        {
                            Console.WriteLine($"PID: {i} - {i.ToString("x")}");

                            var response = driver.ObdMode01.GetPidResponse(i.ToString("x"));
                            Console.WriteLine("*****************************");
                            Console.WriteLine(String.Join(Environment.NewLine, response));
                        }*/

                        var counter = 0;

                        while (_isRunning)
                        {
                            //var rpm = driver.ObdMode01.EngineRpm;
                            //var temp = driver.ObdMode01.EngineCoolantTemperature;
                            //var intakeManifoldPressure = driver.ObdMode01.IntakeManifoldPressure;

                            //rpm = ;
                            //var engineLoad = driver.ObdMode01.EngineLoad;
                            //var massAirflow = driver.ObdMode01.MassAirFlowRate;

                            

                           
                            var signalKData = new Dictionary<string, Func<int, double?>>
                            {
                                { $"propulsion.engine.{_engineName}.revolutions", c =>
                                {
                                    var rpm = driver.ObdMode01.EngineRpm;

                                    if(rpm == 0.0)
                                    {
                                        return null;
                                    }

                                    Log($"RPM={rpm}");
                                    return RoundToTen(rpm) / 60.0;
                                }
                                },
                                { $"propulsion.engine.{_engineName}.temperature", c =>
                                {
                                    if(c % 5 == 0)
                                    {
                                        var temp = driver.ObdMode01.EngineCoolantTemperature;

                                        if(temp == 0.0)
                                        {
                                            return null;
                                        }

                                        return Math.Round(temp +  273.15);
                                    }

                                    return null;
                                } },
                                { $"propulsion.engine.{_engineName}.boostPressure", c =>
                                {
                                    if(c % 5 == 0)
                                    {
                                        var pressure = driver.ObdMode01.IntakeManifoldPressure;

                                        if(pressure == 0.0)
                                        {
                                            return null;
                                        }

                                        return Math.Round(pressure * 1000.0);
                                    }

                                    return null;
                                } }
                            };

                            counter++;

                            if (counter > 100000)
                            {
                                counter = 0;
                            }

                            var data = signalKData.Select(k => new { key = k.Key, value = k.Value(counter) })
                                                  .Where(k => k.value != null)
                                                  .ToDictionary(k => k.key, v => (object)v.value.Value);

                            var line = $"{DateTime.Now.ToShortTimeString()}\t{string.Join(";", data.Select(i => $"{i.Key}={i.Value}"))}, Last data: {(_lastData != null ? (DateTime.UtcNow - _lastData.Value).ToString() : "never")}";

                            Log(line);

                           if (data.Any(i => i.Value != null))
                           {
                               _lastData = DateTime.UtcNow;
                           }


                            SendToSignalK(data);

                            Thread.Sleep(_readInterval);

                            if (_lastData != null && _lastData.Value.AddMilliseconds(_noDataTimeout) < DateTime.UtcNow)
                            {
                                Log($"Connection timeout, disconnecting...");
                                break;
                            }
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

        private double RoundToTen(double value)
        {
            return ((int)Math.Round(value / 10.0f) * 10);
        }

        private void SetUSB(bool powerStatus)
        {
            using(var writer = new StreamWriter(@"/sys/devices/platform/soc/20980000.usb/buspower"))
            {
                writer.Write(powerStatus ? "0" : "1");
            }
        }

        private void Log(object obj)
        {
            Trace.WriteLine(obj);
            //Console.WriteLine(obj?.ToString());

            if (!string.IsNullOrEmpty(_logFile))
            {
                File.AppendText($"{DateTime.Now}:{obj?.ToString()}");
            }
        }

        private void SendToSignalK(Dictionary<string, object> values)
        {
            var cmd = @"{""updates"": [{""$source"": ""SOURCE-HERE"",""values"":[".Replace("SOURCE-HERE", _instance);

            foreach (var item in values)
            {
                var valueString = "null";

                if (item.Value is string)
                {
                    valueString = $"\"{item.Value}\"";
                }
                else if (item.Value is double number)
                {
                    valueString = number.ToString(CultureInfo.InvariantCulture);
                }

                cmd += @"{""path"":""" + item.Key + @""", ""value"": " + valueString + "},";
            }

            cmd = cmd.Substring(0, cmd.Length - 1);

            cmd += "]}]}";

            //Log(cmd);
            
            using (var udp = new UdpClient(_server, _serverPort))
            {
                var data = Encoding.UTF8.GetBytes(cmd);
                udp.Send(data, data.Length);
            }
        }
    }
}
