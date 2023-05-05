package de.thu;

import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.RandomAccessFile;

public class App {

    public static void main(String[] args) {
        System.out.println("Trying to connect to sensor provider...");
        RandomAccessFile pipe = null;
        try {
            pipe = new RandomAccessFile("\\\\.\\pipe\\ble_host_pipe", "r");
            System.out.println("Connected to sensor provider");
        } catch (FileNotFoundException e) {
            e.printStackTrace();
        }
        while(true) {
            try {
                String str = pipe.readLine();
                //System.out.println("Read: " + str);
                if (str == null)
                {
                    continue;
                }
                String[] strarray = str.split(" ");
                short[] data = new short[strarray.length];
                for(int i=0;i<strarray.length;i++)
                {
                    try
                    {
                        data[i] = Short.parseShort(strarray[i]);
                    }
                    catch(NumberFormatException ex)
                    {
                        break;
                    }
                }

                if(data.length>=2 && data[1] == 10)
                {
                    System.out.println(str);
                }


            } catch (FileNotFoundException e) {
                e.printStackTrace();
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
    }
}
