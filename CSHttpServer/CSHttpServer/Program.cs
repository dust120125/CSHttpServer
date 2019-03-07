using System;
using CSHttpServer;

namespace HttpServerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            //port=main port
            //local=local port
            //auth=AuthenticationUserFile (file path) & turn on Authentication
            //log = use log (0-1)
            var port = 80;
            var local = 2222;
            string auth = null;
            var log = true;

            foreach(var s in args)
            {
                var cmd = s.Split('=');
                switch (cmd[0])
                {
                    case "-port":
                        port = int.Parse(cmd[1]);
                        break;
                    case "-local":
                        local = int.Parse(cmd[1]);
                        break;
                    case "-auth":
                        auth = cmd[1];
                        break;
                    case "-log":
                        log = cmd[1] == "1";
                        break;
                }
            }

#if DEBUG
            auth = "users.txt";
            log = true;
#endif

            Console.WriteLine(string.Format("Server port: {0}", port));
            Console.WriteLine(string.Format("Local port: {0}", local));
            if (auth != null)
            {
                Console.WriteLine(string.Format("Authentication on, load users in: {0}", auth));
            }
            else
            {
                Console.WriteLine(string.Format("Authentication off (Security Warning!)"));
            }
            Console.WriteLine(string.Format("Server log: {0}\n", log));

            HttpServer hfs = new HttpServer(port)
            {
                LocalPort = local,
                UseAuthentication = auth != null,
                AuthenticationUserFile = auth,
                UseLog = log
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
            Console.WriteLine("Press any key to leave...");
            Console.ReadKey();
        }
    }
}
