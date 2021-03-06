﻿using DiscordRPC;
using PresenceCommon;
using PresenceCommon.Types;
using System;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Timer = System.Timers.Timer;

namespace VitaPresence_CLI
{
    class Program
    {
        static Timer timer;
        static Socket client;
        static string LastGame = "";
        static Timestamps time = null;
        static DiscordRpcClient rpc;

        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: VitaPresence-CLI <IP> <Client ID>");
                return 1;
            }

            if (!IPAddress.TryParse(args[0], out IPAddress iPAddress))
            {
                Console.WriteLine("Invalid IP");
                return 1;
            }

             rpc = new DiscordRpcClient(args[1]);

            if (!rpc.Initialize())
            {
                Console.WriteLine("Unable to start RPC!");
                return 2;
            }

            IPEndPoint localEndPoint = new IPEndPoint(iPAddress, 0xCAFE);

            timer = new Timer()
            {
                Interval = 15000,
                Enabled = false,
            };
            timer.Elapsed += new ElapsedEventHandler(OnConnectTimeout);

            while (true)
            {
                client = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = 5500,
                    SendTimeout = 5500,
                };

                timer.Enabled = true;

                try
                {
                    IAsyncResult result = client.BeginConnect(localEndPoint, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                    if (!success)
                    {
                        Console.WriteLine(((Socket)result.AsyncState));
                        Console.WriteLine("Could not connect to Server! Retrying...");
                        client.Close();
                    }
                    else
                    {
                        client.EndConnect(result);
                        timer.Enabled = false;

                        DataListen();
                    }
                }
                catch (SocketException)
                {
                    client.Close();
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                }
            }
        }

        private static void DataListen()
        {
            while (true)
            {
                try
                {
                    byte[] bytes = new byte[600];
                    int cnt = client.Receive(bytes);

                    Console.WriteLine("'" + System.Text.Encoding.UTF8.GetString(bytes) + "' received...");

                    Title title = new Title(bytes);
                    if (title.Magic == 0xCAFECAFE)
                    {
                        if (LastGame != title.TitleID)
                        {
                            time = Timestamps.Now;
                        }
                        if ((rpc != null && rpc.CurrentPresence == null) || LastGame != title.TitleID)
                        {
                            rpc.SetPresence(Utils.CreateDiscordPresence(title, time));

                            LastGame = title.TitleID;
                        }
                    }
                    else
                    {
                        if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                        client.Close();
                        return;
                    }
                }
                catch (SocketException)
                {
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                    client.Close();
                    return;
                }
            }
        }

        private static void OnConnectTimeout(object sender, ElapsedEventArgs e)
        {
            LastGame = "";
            time = null;
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
                client.Close();
            
            if (rpc != null && !rpc.IsDisposed)
                rpc.Dispose();   
        }
    }
}
