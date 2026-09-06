using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace WindowsGSM.Plugins
{
    public class Vein : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Vein", // WindowsGSM.XXXX
            author = "raziel7893",
            description = "WindowsGSM plugin for supporting Vein Dedicated Server",
            version = "1.0.2",
            url = "https://github.com/Raziel7893/WindowsGSM.Vein", // Github repository link (Best practice) TODO
            color = "#34FFeb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2131400"; // Game server appId Steam

        // - Standard Constructor and properties
        public Vein(ServerConfig serverData) : base(serverData) => base.serverData = serverData;

        // - Game server Fixed variables
        //public override string StartPath => "VeinServer.exe"; // Game server start path
        public override string StartPath => "Vein\\Binaries\\Win64\\VeinServer-Win64-Test.exe";//"VeinServer.exe";//the test will change sometime, you can use VeinServer.exe as a backup then
        public string BackupExe => "VeinServer.exe";
        public string FullName = "Vein Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // - Game server default values
        public string Port = "7777"; // Default port

        public string Additional = "-log"; // Additional server start parameter

        // TODO: Following options are not supported yet, as ther is no documentation of available options
        public string Maxplayers = "16"; // Default maxplayers        
        public string QueryPort = "27015"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.
        // TODO: Unsupported option
        public string Defaultmap = "default"; // Default map name
        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()



        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            string gameContent = $@"[/Script/Engine.GameSession]
                MaxPlayers={serverData.ServerMaxPlayer}

                [/Script/Vein.VeinGameSession]
                bPublic=True
                ServerName={serverData.ServerName}
                BindAddr=0.0.0.0
                SuperAdminSteamIDs=1234
                AdminSteamIDs=1234
                HeartbeatInterval=5.0
                Password=secret

                [OnlineSubsystemSteam]
                GameServerQueryPort={serverData.ServerQueryPort}
                bVACEnabled=0

                [URL]
                Port={serverData.ServerPort}
                ";
            string configFolder = "Vein\\Saved\\Config\\WindowsServer\\";
            Directory.CreateDirectory(Functions.ServerPath.GetServersServerFiles(serverData.ServerID, configFolder));
            string gameIniFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, configFolder, "Game.ini");
            File.WriteAllText(gameIniFile, gameContent);

            string engineContent = $@"[ConsoleVariables]
                vein.PvP=True
                vein.AISpawner.Enabled=True
                vein.TimeMultiplier=16
                # etc.
                ";

            string engineIniFile = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, configFolder, "Engine.ini");
            File.WriteAllText(engineIniFile, engineContent);
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath}), using backup wrapper";
                shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, BackupExe);
                if (!File.Exists(shipExePath))
                {
                    Error = $"{Path.GetFileName(shipExePath)} Backup exe also not found, crashing";
                    return null;
                }
            }

            //Try gather a password from the gui
            var sb = new StringBuilder();
            sb.Append($"-Port={serverData.ServerPort} ");
            sb.Append($"-QueryPort={serverData.ServerQueryPort} ");
            sb.Append($"-multihome={serverData.ServerIP} ");
            sb.Append($"{serverData.ServerParam} ");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = sb.ToString(),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (serverData.EmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (serverData.EmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
                if (!p.HasExited)
                    p.Kill();
            });
        }
    }
}
