using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace PulsOxiEmulator
{
    class Emulator
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Pulsoximeter");
            SpO2emulated sensor = new SpO2emulated();
            Console.WriteLine("Set heartrate low [p] or high [P]");
            Console.WriteLine("Set SpO2 value low [s] or high [S]");
            Console.WriteLine("Set sensor off [o] or on [O]");
            Console.WriteLine("To set to normal values press [n] or [N] ");
            

            sensor.start();
                        
            Console.WriteLine("Press [ESC] to stop");
            
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey();

                
                if (key.KeyChar.Equals('p') && sensor.setParam("bpm", "low"))
                    Console.WriteLine(" set pulse on 'low'!");
                else if (key.KeyChar.Equals('P') && sensor.setParam("bpm", "high"))
                    Console.WriteLine(" set pulse on 'high'!");
                else if (key.KeyChar.Equals('s') && sensor.setParam("spo2", "low"))
                    Console.WriteLine(" set SpO2 on 'low'!");
                else if (key.KeyChar.Equals('S') && sensor.setParam("spo2", "high"))
                    Console.WriteLine(" set SpO2 on 'high'!");
                else if ((key.KeyChar.Equals('n') || key.KeyChar.Equals('N')) && sensor.setParam("spo2", "norm") && sensor.setParam("bpm", "norm"))
                        Console.WriteLine(" set pulse and SpO2 to 'normal'!");
                else if (key.KeyChar.Equals('o') && sensor.setParam("sensor", "off"))
                    Console.WriteLine(" set sensor off!");
                else if (key.KeyChar.Equals('O') && sensor.setParam("sensor", "on"))
                    Console.WriteLine(" set sensor on!");


            } while (key.Key != ConsoleKey.Escape);
            
            sensor.stop();
            
        }
    }
}
