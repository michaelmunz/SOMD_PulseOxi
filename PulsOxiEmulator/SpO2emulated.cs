using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;

namespace PulsOxiEmulator
{
    public class SpO2emulated 
    {
        private int spLow = 94;
        private int spHigh = 99;
        private int hrMittel = 66;
        protected int hr;
        protected int spo2;

        private NamedPipeServerStream pipeServer = null;
        private BlockingCollection<byte[]> outputQueue = new BlockingCollection<byte[]>();
        private Thread pipeServerThread;

        private const int interval =  1000;
        private Random rand;

        private System.Timers.Timer timer;


        private const byte SEPARATOR = 254;
        private const byte ID1 = 10;
        private const byte ID2 = 85;
        private byte state = 1;

        private byte paketCnt = 0;

        public SpO2emulated()
        {
            hr = 66;
            spo2 = 99;            
            rand = new Random();
        }

        public void start()
        {
            timer = new System.Timers.Timer(interval);
            timer.Elapsed += onTimedEvent;
            timer.Start();

            pipeServerThread = new Thread(runPipeServer);
            pipeServerThread.Start();
        }

        private void runPipeServer()
        {
            while (true)
            {
                pipeServer = new NamedPipeServerStream("ble_host_pipe", PipeDirection.Out, 1);
                Console.WriteLine("Waiting for connection on pipe ble_host_pipe...");
                // Wait for a client to connect
                pipeServer.WaitForConnection();

                Console.WriteLine("Client connected");
                System.IO.StreamWriter writer = new System.IO.StreamWriter(pipeServer);
                writer.AutoFlush = true;
                while (true)
                {
                    try
                    {
                        byte[] data = outputQueue.Take();
                        StringBuilder sb = new StringBuilder();                        
                        foreach (byte b in data)
                        {                            
                            sb.AppendFormat("{0:d2} ", b);
                        }

                        String strdata = sb.ToString();
                        strdata = strdata.Trim();
                        writer.WriteLine(strdata);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error during writing to pipe. Please reconnect.");
                        break;
                    }
                }
                pipeServer.Close();
            }

        }




        private void onTimedEvent(Object source, ElapsedEventArgs e)
        {
            sendData();
            variance();                        
        }

        private void sendData()
        {
            /*
              
            */
            // each 50 pakets with type 8/86 (pletsymography) there is a new pulse paket type 10/85
            // we simulate only those pulse pakets
            
            byte[] paket = { SEPARATOR, ID1, ID2, state, (byte)hr, (byte)spo2, 0, 0, paketCnt, 0 };
            outputQueue.Add(paket);
            paketCnt++;
        }

        public void stop()
        {
            timer.Stop();
        }


        private void variance()
        {
            int var = hr - hrMittel;
            if (Math.Abs(var) > 10)
            {
                if (var > 0)
                {
                    hr -= rand.Next(var);
                }
                else
                {
                    hr += rand.Next(-var);
                }
            }
            else
            {
                hr += rand.Next(5) - 2;
            }

            if (var % 2 == 0 && Math.Abs(var) > 3)
            {
                int sp_soll = (spLow + spHigh) / 2;
                if (spo2 > sp_soll)
                {
                    spo2 -= 1;
                }
                else //if (spo2 < spHigh) {
                {
                    spo2 += 1;
                }
            }
        }

        public bool setParam(string param, string value)
        {
            const String bpm = "BPM";
            const String spo2 = "SpO2";            
            const String vLow = "low";
            const String vHigh = "high";
            const String sensor = "sensor";
            const String vOn = "on";


            bool res = false;

            if (string.Equals(param,bpm,StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, vHigh, StringComparison.OrdinalIgnoreCase))
                {
                    hrMittel = 150;
                    res = true;
                }
                else if (string.Equals(value, vLow, StringComparison.OrdinalIgnoreCase))
                {
                    hrMittel = 40;
                    res = true;
                }
                else
                {
                    hrMittel = 66;
                    res = true;
                }
            }
            else if (string.Equals(param, spo2, StringComparison.OrdinalIgnoreCase)) 
            {
                if (string.Equals(value, vHigh, StringComparison.OrdinalIgnoreCase))
                {
                    spHigh = 100;
                    this.spo2 = 99;
                    spLow = 98;
                    res = true;
                }
                else if (string.Equals(value, vLow, StringComparison.OrdinalIgnoreCase))
                {
                    spHigh = 90;
                    this.spo2 = 85;
                    spLow = 80;
                    res = true;
                }
                else
                {
                    spHigh = 99;
                    this.spo2 = 97;
                    spLow = 94;
                    res = true;
                }
            }
            else if (string.Equals(param, sensor, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, vOn, StringComparison.OrdinalIgnoreCase))
                {
                    state = 0;
                    res = true;
                }                
                else
                {
                    state = 1;
                    res = true;
                }
            }
            return res;
        }
    }
}
