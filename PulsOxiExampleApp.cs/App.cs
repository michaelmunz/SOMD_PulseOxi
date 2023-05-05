using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;

namespace PulsOxiApp
{
    class App
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Trying to connect to sensor provider..."); 
            NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "ble_host_pipe", PipeDirection.In);
            pipeClient.Connect();


            Console.WriteLine("Connected to sensor provider");
            System.IO.StreamReader reader = new System.IO.StreamReader(pipeClient);
            while (true)
            {                
                String str = reader.ReadLine();                                
                if (str == null)
                {
                    continue;
                }
                String[] strarray = str.Split(' ');
                byte[] data = new byte[strarray.Length];
                for(int i=0;i<strarray.Length;i++)
                {
                    try
                    {
                        data[i] = byte.Parse(strarray[i]);
                    }
                    catch(FormatException ex)
                    {
                        break;
                    }
                }

                if(data.Length>=2 && data[1] == 10)
                {
                    Console.WriteLine(str);
                }


                //TODO: parse data and handle data entries separately
                /**
                 * Separator: 254

                ID 8/86 (pletys) 50/s
                0			1	2	3		4		5			6			7	8			9
                Separator	ID	ID2	Pletys	State	Pletys2 q.	Paket Idx	??	??			??

                ID 10/85 (pulse) 1/s
                0			1	2	3		4		5			6			7	8			9
                Separator	ID	ID2	State	Pulse	SpO2		??			??	Paket IDX	??

                */
            }

        }
    }
}
