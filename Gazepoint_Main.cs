using LSL;
using System;
using System.IO;
using System.Net.Sockets;

namespace Gazepoint_LSL_Streamer
{
    class Gazepoint_Main
    {
        // Set up the connection parameters to the Gazepoint server
        const int ServerPort = 4242;
        const string ServerAddr = "127.0.0.1";

        /* Generic function to parse the data from the incoming_data XML stream */
        static double parse_data(string incoming_data, string api_var_name)
        {
            string api_string = api_var_name + "=\"";
            int startindex = incoming_data.IndexOf(api_string) + api_string.Length;
            int endindex = incoming_data.IndexOf("\"", startindex);

            return Double.Parse(incoming_data.Substring(startindex, endindex - startindex));
        }

        static void Main(string[] args)
        {
            // Connect to the Gazepoint server
            bool exit_state = false;
            TcpClient gp3_client;
            NetworkStream data_feed;
            StreamWriter data_write;
            var prev_time_val = 0.00;
            String incoming_data = "";

            ConsoleKeyInfo keybinput;
            // Try to create client object, return if no server found
            try
            {
                gp3_client = new TcpClient(ServerAddr, ServerPort);
            }
            catch (Exception e)
            {
                Console.WriteLine("Specific error");
                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("{0}", e);
                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("Failed to connect to the localhost server.");
                Console.WriteLine("Did you forget to turn on the Gazepoint Control software?");
                Console.ReadKey();
                return;
            }

            // Create the streams & channels
            liblsl.StreamInfo info = new liblsl.StreamInfo("Gazepoint_Eyetracker", "Gaze", 9, 150, liblsl.channel_format_t.cf_double64, "GazepointStream");
            var channels = info.desc().append_child("channels");
            channels.append_child("channel").append_child_value("label", "TIME_VAL").append_child_value("unit", "seconds").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "FPOGX").append_child_value("unit", "percent").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "FPOGY").append_child_value("unit", "percent").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "FPOG_VALID").append_child_value("unit", "boolean").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "LPMM").append_child_value("unit", "mm").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "RPMM").append_child_value("unit", "mm").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "BKID").append_child_value("unit", "integer").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "BKDUR").append_child_value("unit", "seconds").append_child_value("type", "gaze");
            channels.append_child("channel").append_child_value("label", "BKPMIN").append_child_value("unit", "integer").append_child_value("type", "gaze");
            liblsl.StreamOutlet outlet = new liblsl.StreamOutlet(info);
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("  LSL Stream outlet created: \"Gazepoint_EyeTracker\"");
            Console.WriteLine("-----------------------------------------------------");

            // Load the read and write streams
            data_feed = gp3_client.GetStream();
            data_write = new StreamWriter(data_feed);

            // Setup the data records
            data_write.Write("<SET ID=\"ENABLE_SEND_TIME\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_COUNTER\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_BLINK\" STATE=\"1\" />\r\n");
            //data_write.Write("<SET ID=\"ENABLE_SEND_POG_BEST\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_PUPILMM\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_LEFT_PUPIL\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_RIGHT_PUPIL\" STATE=\"1\" />\r\n");
            //data_write.Write("<SET ID=\"ENABLE_SEND_CURSOR\" STATE=\"1\" />\r\n");
            data_write.Write("<SET ID=\"ENABLE_SEND_DATA\" STATE=\"1\" />\r\n");

            // Flush the buffer out the socket
            data_write.Flush();

            do
            {
                int ch = data_feed.ReadByte();
                if (ch != -1)
                {
                    incoming_data += (char)ch;

                    // find string terminator ("\r\n") 
                    if (incoming_data.IndexOf("\r\n") != -1)
                    {
                        // only process DATA RECORDS, ie <REC .... />
                        if (incoming_data.IndexOf("<REC") != -1)
                        {
                            // Parse all of the data
                            double time_val = parse_data(incoming_data, "TIME");    // Timestamp
                            double count = parse_data(incoming_data, "CNT");        // Counter
                            double fpogx = parse_data(incoming_data, "FPOGX");      // Fixed Point of Gaze X
                            double fpogy = parse_data(incoming_data, "FPOGY");      // Fixed Point of Gaze Y
                            double fpog_valid = parse_data(incoming_data, "FPOGV"); // Fixed Point of Gaze Valid
                            double lpmm = parse_data(incoming_data, "LPMM");        // Left Pupil Diameter (mm)
                            double rpmm = parse_data(incoming_data, "RPMM");        // Right Pupil Diameter (mm)
                            double bkid = parse_data(incoming_data, "BKID");        // Blink ID (0 or 1) 
                            double bkdur = parse_data(incoming_data, "BKDUR");      // Blink Duration (s)
                            double bkpmin = parse_data(incoming_data, "BKPMIN");    // Blink per Minute

                            // Send to LSL
                            double[] data = new double[] { time_val, fpogx, fpogy, fpog_valid, lpmm, rpmm, bkid, bkdur, bkpmin };
                            outlet.push_sample(data);

                            // Calculate RT sampling rate
                            var Fs = 1 / (time_val - prev_time_val);
                            prev_time_val = time_val;

                            // Print the current data to console
                            Console.Write("\rGaze: ({0:F2},{1:F2}) \tTimestamp: {2:F1}s\tCount: {3}\tFs: {4:F1}Hz          ",
                                                  fpogx, fpogy, time_val, count, Fs);
                            
                        }

                        // TODO: change this to a proper buffer? is this a buffer?
                        incoming_data = "";
                    }
                }

                // Check if key press - if so, quit
                if (Console.KeyAvailable == true)
                {
                    keybinput = Console.ReadKey(true);
                    if (keybinput.Key == ConsoleKey.Q)
                    {
                        exit_state = true;
                    }
                }
            }
            while (exit_state == false);

            // Close streams
            data_write.Close();
            data_feed.Close();
            gp3_client.Close();
        }
    }
}