using System.IO;
using Microsoft.Win32;

namespace IsaacSavegameToLua
{
    class Program
    {
        static int curUserID = 0;
        static string lastUsername = "";
        static string platform = "(Undefined)";

        static void Main(string[] args)
        {
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine("~~~~~~~~~~~~~~~~~ Isaac Savegame Reader for EID ~~~~~~~~~~~~~");
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~ Compatible with Repentance ~~~~~~~~~~~~~~");
            Console.WriteLine("~~~ For Steam Users: Login to force a User to be selected ~~~");
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            string saveGamePath = getSteamSavegamePath();
            if (saveGamePath == string.Empty)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~ Manual Mode ~~~~~~~~~~~~~~~~~~~");
                Console.WriteLine("In manual mode, you need to enter the filepath to your samegames yourself.");
                Console.WriteLine("Please enter the folder path where your \"rep_persistentgamedata1.dat\" file is located:");
                // reset steam infos
                curUserID = 0;
                lastUsername = "";
                platform = "(Undefined)";

                saveGamePath = @"" + Console.ReadLine();
                if (File.Exists(saveGamePath))
                {
                    // strip the filename from the path if present
                    saveGamePath = Path.GetDirectoryName(saveGamePath);
                }

                Console.WriteLine("Searching savegames in: " + saveGamePath);
                if (!File.Exists(saveGamePath + "\\rep_persistentgamedata1.dat"))
                {
                    Console.WriteLine("The folder \"" + saveGamePath + "\" does not contain a \"rep_persistentgamedata1.dat\" file. Aborting...");
                    Console.WriteLine("Press any key to close the program");
                    Console.ReadKey();
                    return;
                }
            }

            using (StreamWriter writetext = new StreamWriter("eid_savegames.lua"))
            {
                writetext.WriteLine("-- This file was auto-generated using \"Isaac Savegame Reader\" by Wofsauge");
                writetext.WriteLine("-- If you experience desynchronizations, please run the tool again");

                writetext.WriteLine("\nEID.SaveGame = {}");
                writetext.WriteLine("EID.SaveGame.Platform = \"" + platform + "\" -- platform of the game (Steam, Epic, Others, ...)");
                writetext.WriteLine("EID.SaveGame.UserID = " + curUserID + " -- Steam ID of the User");
                writetext.WriteLine("EID.SaveGame.UserName = \"" + lastUsername + "\" -- Name of the steam user\n");

                for (int i = 1; i < 4; i++)
                {
                    writetext.WriteLine("EID.SaveGame[" + i + "] = {");
                    Console.WriteLine("Reading save game slot " + i + "...");
                    Dictionary<int, bool> touchState = readRepSavegame(saveGamePath + "\\rep_persistentgamedata" + i + ".dat");

                    writetext.Write("\tItemCollection = {\n\t\t");
                    foreach (var item in touchState)
                    {
                        writetext.Write("[" + item.Key + "]=" + (item.Value ? "true" : "false") + ", ");
                    }
                    writetext.WriteLine("\n\t},\n");
                    
                    writetext.Write("\tItemNeedsPickup = {\n\t\t");
                    foreach (var item in touchState)
                    {
                        if (item.Key > 0 && ! item.Value){
                            writetext.Write("[" + item.Key + "]=true, ");
                        }
                    }
                    writetext.WriteLine("\n\t},\n}\n");
                }
            }

            Console.WriteLine("SUCCESS! Savegame infos successfully added to EID");
            Console.WriteLine("Press any key to close the program");
            Console.ReadKey();
        }

        static string getSteamSavegamePath()
        {
            string steamRegistry = "HKEY_CURRENT_USER\\SOFTWARE\\Valve\\Steam";
            string steamFilepath = (string)Registry.GetValue(steamRegistry, "SteamPath", "");
            if (steamFilepath == string.Empty)
            {
                Console.WriteLine("Steam installation path not found! Falling back to manual mode...");
                return "";
            }
            Console.WriteLine("Steam install path found: " + steamFilepath);

            string userDataPath = steamFilepath + "\\userdata";

            curUserID = (int)Registry.GetValue(steamRegistry + "\\ActiveProcess", "ActiveUser", 0);

            if (curUserID == 0)
            {
                Console.WriteLine("Steam is currently not running. Trying to find user with Isaac savedata...");
                curUserID = getUserIDFromPath(userDataPath);
                if (curUserID == 0)
                {
                    Console.WriteLine("No steam user played Isaac :( Falling back to manual mode...");
                    return "";
                }
            }
            Console.WriteLine("Steam userid found: " + curUserID);

            string saveGamePath = userDataPath + "\\" + curUserID + "\\250900\\remote";
            lastUsername = getUserNameFromID(userDataPath + "\\" + curUserID, curUserID);
            platform = "Steam";
            return saveGamePath;
        }

        static int getUserIDFromPath(string filepath)
        {
            foreach (string userDir in Directory.GetDirectories(filepath))
            {
                string folderName = userDir.Replace(filepath + "\\", "");
                int userID;
                bool isNumeric = int.TryParse(folderName, out userID);

                if (!isNumeric)
                    continue;

                if (File.Exists(userDir + "\\250900\\remote\\rep_persistentgamedata1.dat"))
                {
                    Console.WriteLine("Found user with a isaac savegame");
                    return userID;
                }
            }
            return 0;
        }

        static string getUserNameFromID(string filepath, int id)
        {
            if (File.Exists(filepath + "\\config\\localconfig.vdf"))
            {
                using (StreamReader file = new StreamReader(filepath + "\\config\\localconfig.vdf"))
                {
                    Console.WriteLine("Found steam userconfig... trying to find a username...");
                    string ln;
                    while ((ln = file.ReadLine()) != null)
                    {
                        if (ln.Contains("PersonaName"))
                        {
                            string username = ln.Trim().Replace("\"", "").Replace("PersonaName", "").Replace("\t", "");
                            Console.WriteLine("Found steam Username: " + username);
                            file.Close();
                            return username;
                        }
                    }
                    file.Close();
                }
            }
            Console.WriteLine("[Warning] No steam username found. Proceeding with default value...");
            return "(No Username found)";
        }


        static Dictionary<int, bool> readRepSavegame(string filepath)
        {
            Dictionary<int, bool> itemTouchStatus = new Dictionary<int, bool>();
            FileStream fs = new FileStream(filepath, FileMode.Open);

            int itemTouchLocation = Convert.ToInt32("0x00000AB6", 16);
            int hexIn;
            int itemCount = 0;
            string firstBitItemCount = "";
            for (int i = 0; (hexIn = fs.ReadByte()) != -1; i++)
            {
                String hex = string.Format("{0:X2}", hexIn);
                if (i == itemTouchLocation)
                {
                    firstBitItemCount = hex;
                }
                if (i == itemTouchLocation + 1)
                {
                    itemCount = Convert.ToInt32(hex + firstBitItemCount, 16);
                }
                if (i > itemTouchLocation + 2 && i <= itemTouchLocation + itemCount + 2)
                {
                    itemTouchStatus.Add(i - itemTouchLocation - 3, hex == "01");
                }
            }
            return itemTouchStatus;
        }
    }
}