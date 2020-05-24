﻿using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Collections.Generic;

namespace Reecon
{
    class HTTP
    {
        public static string GetInfo(string ip, int port, bool isHTTPS)
        {
            var httpInfo = GetHTTPInfo(ip, port, isHTTPS);
            if (httpInfo == (0, null, null, null, null))
            {
                return "";
            }
            string portData = FormatResponse(httpInfo.StatusCode, httpInfo.Title, httpInfo.DNS, httpInfo.Headers, httpInfo.SSLCert);
            string robotsFile = CheckRobots(ip, port, isHTTPS);
            portData = robotsFile + portData;
            string passwdFile = CheckPasswd(ip, port);
            if (passwdFile != "")
            {
                portData += Environment.NewLine + passwdFile + Environment.NewLine;
            }
            return portData;
        }

        private static (HttpStatusCode StatusCode, string Title, string DNS, WebHeaderCollection Headers, X509Certificate2 SSLCert) GetHTTPInfo(string ip, int port, bool isHTTPS)
        {
            string pageTitle = "";
            string pageData = "";
            string dns = "";
            string urlPrefix = "http";
            HttpStatusCode statusCode = new HttpStatusCode();
            if (isHTTPS)
            {
                urlPrefix += "s";
            }
            WebHeaderCollection headers = null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlPrefix + "://" + ip + ":" + port);
            try
            {
                // Ignore invalid SSL Cert
                request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                request.AllowAutoRedirect = false;

                // Can crash here due to a WebException on 401 Unauthorized / 403 Forbidden errors, so have to do some things twice
                request.Timeout = 5000;
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    statusCode = response.StatusCode;
                    dns = response.ResponseUri.DnsSafeHost;
                    headers = response.Headers;
                    using (StreamReader readStream = new StreamReader(response.GetResponseStream()))
                    {
                        pageData = readStream.ReadToEnd();
                    }
                    response.Close();
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                {
                    // WebClient wc = new WebClient();
                    // string someString = wc.DownloadString("https://" + ip + ":" + port);
                    return (statusCode, null, null, null, null);
                }
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                statusCode = response.StatusCode;
                dns = response.ResponseUri.DnsSafeHost;
                headers = response.Headers;
                using (StreamReader readStream = new StreamReader(response.GetResponseStream()))
                {
                    pageData = readStream.ReadToEnd();
                }
                response.Close();
            }
            catch (Exception ex)
            {
                // Something went really wrong...
                Console.WriteLine("GetHTTPInfo - Fatal Woof :( - " + ex.Message);
                return (statusCode, null, null, null, null);
            }

            if (pageData.Contains("<title>") && pageData.Contains("</title>"))
            {
                pageTitle = pageData.Remove(0, pageData.IndexOf("<title>") + "<title>".Length);
                pageTitle = pageTitle.Substring(0, pageTitle.IndexOf("</title>"));
            }
            X509Certificate2 cert = null;
            if (request.ServicePoint.Certificate != null)
            {
                cert = new X509Certificate2(request.ServicePoint.Certificate);
            }
            return (statusCode, pageTitle, dns, headers, cert);
        }

        private static string CheckRobots(string ip, int port, bool isHTTPS)
        {
            string returnText = "";
            string urlPrefix = "http";
            if (isHTTPS)
            {
                urlPrefix += "s";
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlPrefix + "://" + ip + ":" + port + "/robots.txt");
            // Ignore invalid SSL Cert
            request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            try
            {
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        returnText += "- Robots File exists: " + urlPrefix + "://" + ip + ":" + port + "/robots.txt" + Environment.NewLine;
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    HttpWebResponse response = (HttpWebResponse)ex.Response;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        returnText += "- Robots File exists: " + urlPrefix + "://" + ip + ":" + port + "/robots.txt" + Environment.NewLine;
                    }
                }
                else
                {
                    Console.WriteLine("CheckRobots - Something weird happened: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CheckRobots - Fatal Woof: " + ex.Message);
            }

            return returnText;
        }

        private static string CheckPasswd(string ip, int port)
        {
            string result = General.BannerGrab(ip, port, "GET /../../../../../../etc/passwd" + Environment.NewLine + Environment.NewLine, 2500);
            if (result.Contains("root"))
            {
                return "- Hidden Passwd File Found (GET /../../../../../../etc/passwd)!" + Environment.NewLine + result;
                // Need to format this better...

            }
            return "";
        }

        private static string FormatResponse(HttpStatusCode StatusCode, string Title, string DNS, WebHeaderCollection Headers, X509Certificate2 SSLCert)
        {
            string responseText = "";

            if (StatusCode != HttpStatusCode.OK)
            {
                // There's a low chance that it will return a StatusCode that is not in the HttpStatusCode list in which case (int)StatusCode will crash
                try
                {
                    responseText += "- Non-OK Status Code: " + (int)StatusCode + " " + StatusCode + Environment.NewLine;
                }
                catch
                {
                    responseText += "- Unknown Status Code: " + " " + StatusCode + Environment.NewLine;
                }

                if (StatusCode != HttpStatusCode.OK)
                {
                    if (Headers != null && Headers.Get("Location") != null)
                    {
                        responseText += "- Location: " + Headers.Get("Location") + Environment.NewLine;
                    }
                }
            }
            if (!string.IsNullOrEmpty(Title))
            {
                responseText += "- Page Title: " + Title + Environment.NewLine;
            }
            if (!string.IsNullOrEmpty(DNS))
            {
                responseText += "- DNS: " + DNS + Environment.NewLine;
            }
            if (Headers != null)
            {
                List<string> headerList = Headers.AllKeys.ToList();
                if (headerList.Contains("Server"))
                {
                    headerList.Remove("Server");
                    responseText += "- Server: " + Headers.Get("Server") + Environment.NewLine;
                }
                if (headerList.Contains("X-Powered-By"))
                {
                    headerList.Remove("X-Powered-By");
                    responseText += "- X-Powered-By: " + Headers.Get("X-Powered-By") + Environment.NewLine;
                }
                if (headerList.Contains("WWW-Authenticate"))
                {
                    headerList.Remove("WWW-Authenticate");
                    responseText += "- WWW-Authenticate: " + Headers.Get("WWW-Authenticate") + Environment.NewLine;
                }
                if (headerList.Contains("kbn-name"))
                {
                    headerList.Remove("kbn-name");
                    responseText += "- kbn-name: " + Headers.Get("kbn-name") + Environment.NewLine;
                }
                if (headerList.Contains("Content-Type"))
                {
                    string contentType = Headers.Get("Content-Type");
                    if (contentType != "text/html")
                    {
                        // A unique content type - Might be interesting
                        responseText += "- Content-Type: " + Headers.Get("Content-Type") + Environment.NewLine;
                    }
                }
                responseText += "- Other Headers: " + string.Join(",", headerList) + Environment.NewLine;
            }
            if (SSLCert != null)
            {
                string certIssuer = SSLCert.Issuer;
                string certSubject = SSLCert.Subject;
                // string certAltName = SSLCert.SubjectName.Name;
                responseText += "- SSL Cert Issuer: " + certIssuer + Environment.NewLine;
                responseText += "- SSL Cert Subject: " + certSubject + Environment.NewLine;
                if (SSLCert.Extensions != null)
                {
                    X509ExtensionCollection extensionCollection = SSLCert.Extensions;
                    foreach (X509Extension extension in extensionCollection)
                    {
                        string extensionType = extension.Oid.FriendlyName;
                        if (extensionType == "Subject Alternative Name")
                        {

                            AsnEncodedData asndata = new AsnEncodedData(extension.Oid, extension.RawData);
                            List<string> formattedValues = asndata.Format(true).Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
                            string itemList = "";
                            foreach (string item in formattedValues)
                            {
                                string theItem = item;
                                theItem = theItem.Replace("DNS Name=", "");
                                if (theItem.Contains("("))
                                {
                                    theItem = theItem.Remove(0, theItem.IndexOf("(") + 1).Replace(")", "");
                                    itemList += theItem + ",";
                                }
                                else
                                {
                                    itemList += theItem + ",";
                                }
                            }
                            itemList = itemList.Trim(',');
                            responseText += "- Subject Alternative Name: " + itemList + Environment.NewLine;
                        }
                    }
                }
            }
            responseText = responseText.TrimEnd(Environment.NewLine.ToCharArray()); // Clean off any redundant newlines
            return responseText;
        }
    }
}
