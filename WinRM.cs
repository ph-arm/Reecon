﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Reecon
{
    class WinRM
    {
        public static string GetInfo(string ip, int port)
        {
            string returnInfo = "";

            WebClient wc = new WebClient();
            wc.Headers.Add("Content-Type", "application/soap+xml;charset=UTF-8");
            // 47001 - No Response?
            // Test: CSL Potato
            Byte[] byteData = Encoding.ASCII.GetBytes("dsadsasa");
            try
            {
                wc.UploadData("http://" + ip + ":" + port + "/wsman", byteData);
            }
            catch (WebException wex)
            {
                if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    foreach (string item in wex.Response.Headers)
                    {
                        string headerName = item;
                        string headerValue = wex.Response.Headers[headerName];
                        if (headerName == "Server")
                        {
                            returnInfo += "- Server: " + headerValue + Environment.NewLine;
                        }
                        else if (headerName == "WWW-Authenticate")
                        {
                            returnInfo += "- Authentication Methods: " + headerValue;
                        }
                    }
                }
            }

            return returnInfo;
        }

        public static void WinRMBrute(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("WinRM Brute Usage: reecon -winrm-brute IP Userfile Passfile");
                return;
            }
            string ip = args[1];
            string userFile = args[2];
            string passFile = args[3];

            // Windows: Only files
            if (General.GetOS() == General.OS.Windows)
            {
                if (!File.Exists(userFile))
                {
                    Console.WriteLine("Unable to find UserFile: " + userFile);
                    return;
                }
                if (!File.Exists(passFile))
                {
                    Console.WriteLine("Unable to find Passfile: " + passFile);
                    return;
                }
                WinRMBrute_Windows(ip, userFile, passFile);
            }
            // Linux takes either
            else
            {
                WinRMBrute_Linux(ip, userFile, passFile);
            }
        }

        private static void WinRMBrute_Windows(string ip, string userFile, string passFile)
        {
            List<string> userList = File.ReadAllLines(userFile).ToList();
            List<string> passList = File.ReadAllLines(passFile).ToList();

            // Perms
            List<string> permLines = General.GetProcessOutput("powershell", @"Set-Item WSMan:\localhost\Client\TrustedHosts " + ip + " -Force");
            if (permLines.Count != 0)
            {
                if (permLines[0].Trim() == "Set-Item : Access is denied.")
                {
                    Console.WriteLine("You need to run Reecon in an Administrative console for this functionality");
                    return;
                }
            }
            foreach (string user in userList)
            {
                foreach (string pass in passList)
                {
                    Console.Write("Testing " + user + ":" + pass + " - ");
                    List<string> processResult = General.GetProcessOutput("powershell", "$creds = New-Object System.Management.Automation.PSCredential -ArgumentList ('" + user + "', (ConvertTo-SecureString \"" + pass + "\" -AsPlainText -Force)); Test-WSMan -ComputerName " + ip + " -Credential $creds -Authentication Negotiate -erroraction SilentlyContinue");
                    if (processResult.Count != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Success!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Failed");
                        Console.ForegroundColor = ConsoleColor.White;

                    }
                }
            }
            General.RunProcess("powershell", @"Set-Item WSMan:\localhost\Client\TrustedHosts '' -Force");
        }

        private static void WinRMBrute_Linux(string ip, string userFile, string passFile)
        {
            if (General.IsInstalledOnLinux("crackmapexec", ""))
            {
                Console.WriteLine("Starting - Please wait...");
                General.RunProcessWithOutput("crackmapexec", "winrm " + ip + " -u " + userFile + " -p " + passFile);
            }
            else
            {
                Console.WriteLine("This requires crackmapexec -> https://github.com/byt3bl33d3r/CrackMapExec/releases");
            }
        }
    }
}
