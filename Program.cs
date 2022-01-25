using System;
using Microsoft.Win32;

namespace IsaacSavegameToLua
{
    class Program
    {
        static void Main(string[] args)
        {
            string steamRegistry = "HKEY_CURRENT_USER\\SOFTWARE\\Valve\\Steam";
            string steamFilepath = (string)Registry.GetValue(steamRegistry, "SteamPath", "");
            if (steamFilepath == string.Empty)
            {
                Console.WriteLine("Steam installation path not found! Aborting");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Steam install path found: " + steamFilepath);

            string userDataPath = steamFilepath + "\\userdata";

            int curUserID = (int)Registry.GetValue(steamRegistry + "\\ActiveProcess", "ActiveUser", 0);

            if (curUserID == 0)
            {
                Console.WriteLine("Steam is currently not running. Trying to find user with Isaac savedata...");
                curUserID = getUserIDFromPath(userDataPath);
                if (curUserID == 0)
                {
                    Console.WriteLine("No steam user played Isaac :( Aborting... ");
                    Console.ReadKey();
                    return;
                }
            }
            Console.WriteLine("Steam userid found: " + curUserID);

            string saveGamePath = userDataPath + "\\" + curUserID + "\\250900\\remote";

            for (int i = 1; i < 2; i++)
            {
                Console.WriteLine("Reading save game slot " + i + "...");
                Dictionary<int, bool> touchState = readRepSavegame(saveGamePath + "\\rep_persistentgamedata" + i + ".dat");
            }

            Console.WriteLine("Press any key to close the program");
            //Console.ReadKey();
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
            foreach (var item in itemTouchStatus)
            {
                Console.WriteLine(item.Key + " " + item.Value);
            }
            return itemTouchStatus;
        }
    }
}