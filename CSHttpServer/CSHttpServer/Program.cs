using System;
using CSHttpServer;

namespace HttpServerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpServer hfs = new HttpServer(80)
            {
                LocalPort = 2222,
                UseAuthentication = true,
                AuthenticationUserFile = "Users.txt",
                UseLog = true
            };
            hfs.Start();

            while (true)
            {
                var cmd = Console.ReadLine();
                var cmdAtr = cmd.Split(' ');
                switch (cmdAtr[0])
                {
                    case "stop":
                        hfs.Stop();                        
                        break;
                }
                if (cmdAtr[0] == "stop") break;
            }
        }
    }
}
