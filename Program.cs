using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace IsaacSavegameToLua
{
    class Program
    {
        static int curUserID = 0;
        static string lastUsername = "";
        static string platform = "(Undefined)";
        static int[] ignoreIDs = { 0, 43, 61, 235, 587, 613, 620, 630, 648, 662, 666, 718 };

        static void Main(string[] args)
        {
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine("~~~~~~~~~~~~~~ Isaac Savegame Reader for EID ~~~~~~~~~~~~~~~~");
            Console.WriteLine("~~~~~~ Compatible with AB+, Repentance and Repentance+ ~~~~~~");
            Console.WriteLine("~~~ For Steam Users: Login to force a User to be selected ~~~");
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            string saveGamePath = string.Empty;
            if (Array.IndexOf(args, "-manual") < 0)
            {
                saveGamePath = GetSteamSavegamePath();
            }

            if (saveGamePath == string.Empty)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~ Manual Mode ~~~~~~~~~~~~~~~~~~~");
                Console.WriteLine("In manual mode, you need to enter the filepath to your samegames yourself.");
                Console.WriteLine("Please enter the folder path where your \"abp_persistentgamedata1.dat\" or \"rep_persistentgamedata1.dat\" file is located:");
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
                if (!File.Exists(saveGamePath + "\\rep+persistentgamedata1.dat") &&
                    !File.Exists(saveGamePath + "\\rep_persistentgamedata1.dat") &&
                    !File.Exists(saveGamePath + "\\abp_persistentgamedata1.dat"))
                {
                    Console.WriteLine("The folder \"" + saveGamePath + "\" does not contain a \"rep+persistentgamedata1.dat\", \"rep_persistentgamedata1.dat\" or \"abp_persistentgamedata1.dat\" file. Aborting...");
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
                    Dictionary<int, bool> touchState = ReadSavegame(saveGamePath, i);

                    writetext.Write("\tItemCollection = {\n\t\t");
                    foreach (var item in touchState)
                    {
                        if (Array.IndexOf(ignoreIDs, item.Key) < 0)
                        {
                            writetext.Write("[" + item.Key + "]=" + (item.Value ? "true" : "false") + ", ");
                        }
                    }
                    writetext.WriteLine("\n\t},\n");

                    writetext.Write("\tItemNeedsPickup = {\n\t\t");
                    foreach (var item in touchState)
                    {
                        if (Array.IndexOf(ignoreIDs, item.Key) < 0 && !item.Value)
                        {
                            writetext.Write("[" + item.Key + "]=true, ");
                        }
                    }
                    writetext.WriteLine("\n\t},\n}\n");
                }
            }

            Console.WriteLine("\n~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine("SUCCESS! Savegame infos successfully added to EID\n");
            Console.WriteLine("Press any key to close the program");
            Console.ReadKey();
        }

        static string GetSteamSavegamePath()
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
                curUserID = GetUserIDFromPath(userDataPath);
                if (curUserID == 0)
                {
                    Console.WriteLine("No steam user played Isaac :( Falling back to manual mode...");
                    return "";
                }
            }
            Console.WriteLine("Steam userid found: " + curUserID);

            string saveGamePath = userDataPath + "\\" + curUserID + "\\250900\\remote";
            lastUsername = GetUserNameFromID(userDataPath + "\\" + curUserID, curUserID);
            platform = "Steam";
            return saveGamePath;
        }

        static int GetUserIDFromPath(string filepath)
        {
            foreach (string userDir in Directory.GetDirectories(filepath))
            {
                string folderName = userDir.Replace(filepath + "\\", "");
                int userID;
                bool isNumeric = int.TryParse(folderName, out userID);

                if (!isNumeric)
                    continue;

                string checkDir = userDir + "\\250900\\remote\\";
                if (File.Exists(checkDir + "rep_persistentgamedata1.dat") || File.Exists(checkDir + "abp_persistentgamedata1.dat"))
                {
                    Console.WriteLine("Found user with a isaac savegame");
                    return userID;
                }
            }
            return 0;
        }

        static string GetUserNameFromID(string filepath, int id)
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

        static string GetCorrectSavefile(string steamCloudPath, int saveID, string dlc)
        {
            string userFolder = System.Environment.GetEnvironmentVariable("USERPROFILE");
            string file, altPath;
            switch (dlc)
            {
                case "rep+":
                    file = steamCloudPath + "\\rep+persistentgamedata" + saveID + ".dat";
                    altPath = userFolder + "\\Documents\\My Games\\Binding of Isaac Repentance+\\persistentgamedata" + saveID + ".dat";
                    break;
                case "rep":
                    file = steamCloudPath + "\\rep_persistentgamedata" + saveID + ".dat";
                    altPath = userFolder + "\\Documents\\My Games\\Binding of Isaac Repentance\\persistentgamedata" + saveID + ".dat";
                    break;
                case "ab+":
                    file = steamCloudPath + "\\abp_persistentgamedata" + saveID + ".dat";
                    altPath = userFolder + "\\Documents\\My Games\\Binding of Isaac Afterbirth+\\persistentgamedata" + saveID + ".dat";
                    break;
                default:
                    return String.Empty;
            }
            if (!File.Exists(file) && !File.Exists(altPath))
            {
                // no file exists
                return String.Empty;
            }
            if (!File.Exists(file) && File.Exists(altPath))
            {
                // only My Games save exists
                return altPath;
            }
            else if (File.Exists(file) && !File.Exists(altPath))
            {
                // only steam cloud exists
                return file;
            }
            // get the one with most recent edit time
            return File.GetLastWriteTime(file) >= File.GetLastWriteTime(altPath) ? file : altPath;
        }

        static Dictionary<int, bool> ReadSavegame(string filepath, int saveID)
        {
            Dictionary<int, bool> itemTouchStatus = new Dictionary<int, bool>();
            string file = GetCorrectSavefile(filepath, saveID, "rep+");
            int itemTouchLocation = Convert.ToInt32("0x00000B1D", 16);
            string dlc = "Repentance+";
            if (file == String.Empty)
            {
                file = GetCorrectSavefile(filepath, saveID, "rep");
                itemTouchLocation = Convert.ToInt32("0x00000AB6", 16);
                dlc = "Repentance";
            }
            if (file == String.Empty)
            {
                file = GetCorrectSavefile(filepath, saveID, "ab+");
                itemTouchLocation = Convert.ToInt32("0x00000560", 16);
                dlc = "Afterbirth+";
                if (!File.Exists(file))
                {
                    return itemTouchStatus;
                }
            }
            Console.WriteLine("\nReading " + dlc + " save game slot " + saveID + "...");

            Console.WriteLine("Filepath: " + file);

            FileStream fs = new FileStream(file, FileMode.Open);
            int hexIn;
            int itemCount = 0;
            string firstBitItemCount = "";
            for (int i = 0; (hexIn = fs.ReadByte()) != -1; i++)
            {
                string hex = string.Format("{0:X2}", hexIn);
                if (i == itemTouchLocation)
                {
                    firstBitItemCount = hex;
                }
                if (i == itemTouchLocation + 1)
                {
                    itemCount = Convert.ToInt32(hex + firstBitItemCount, 16);
                    Console.WriteLine("Writing data for " + itemCount + " items...");
                }
                if (i > itemTouchLocation + 3 && i <= itemTouchLocation + itemCount + 3)
                {
                    itemTouchStatus.Add(i - itemTouchLocation - 4, hex == "01");
                }
            }
            return itemTouchStatus;
        }
    }

}
