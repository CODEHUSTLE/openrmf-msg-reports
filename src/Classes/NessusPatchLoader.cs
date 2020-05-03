// Copyright (c) Cingulara LLC 2020 and Tutela LLC 2020. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using openrmf_msg_report.Models;
using System.Xml;
using System;

namespace openrmf_msg_report.Classes
{
    public static class NessusPatchLoader
    {        
        public static List<NessusPatchData> LoadPatchData(string rawNessusPatchFile) {
            List<NessusPatchData> myPatchData = new List<NessusPatchData>();            
            rawNessusPatchFile = rawNessusPatchFile.Replace("\t","");
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawNessusPatchFile);

            XmlNodeList reportList = xmlDoc.GetElementsByTagName("Report");
            XmlNodeList reportHostList = xmlDoc.GetElementsByTagName("ReportHost");
            // ensure all three are valid otherwise this XML is junk
            if (reportList != null && reportHostList != null) {
                // get the Report name
                string reportName = "Unknown";
                if (reportList.Count >= 1)
                    reportName = getReportName(reportList.Item(0));
                // now get the ReportHost Listing
                if (reportHostList.Count > 0) {
                    myPatchData = getReportHostListing(reportName, reportHostList);
                }
            }            
            return myPatchData;
        }

        private static string getReportName(XmlNode node) {
            XmlAttributeCollection colAttributes = node.Attributes;
            string title = "";
            foreach (XmlAttribute attr in colAttributes) {
                if (attr.Name == "name") {
                    title = attr.Value;
                }
                break;
            }
            return title;
        }

        private static List<NessusPatchData> getReportHostListing(string reportName, XmlNodeList nodes) {
            List<NessusPatchData> listPatchData = new List<NessusPatchData>();
            NessusPatchData patchData = new NessusPatchData();

            XmlAttributeCollection colAttributes;
            string hostname = "";
            string netbiosname = "";
            string operatingSystem = "";
            string systemType = "";
            bool credentialed = false;
            string ipAddress = "";
            string scanVersion = "";

            foreach (XmlNode node in nodes) {
                // reset the variables for each reporthost listing
                hostname = "";
                netbiosname = "";
                operatingSystem = "";
                systemType = "";
                credentialed = false;
                ipAddress = "";
                colAttributes = node.Attributes;
                foreach (XmlAttribute attr in colAttributes) {
                    if (attr.Name == "name") {
                        hostname = SanitizeHostname(attr.Value);
                        break;
                    }
                }              
                if (node.ChildNodes.Count > 0) {
                    foreach (XmlElement child in node.ChildNodes) {
                        if (child.Name == "HostProperties") {
                            // for each child node in here
                            netbiosname = "";
                            operatingSystem = "";
                            systemType = "";
                            credentialed = false;
                            ipAddress = "";
                            foreach (XmlElement hostChild in child.ChildNodes) {
                                // get the child
                                foreach (XmlAttribute childAttr in hostChild.Attributes) {
                                    // cycle through attributes where attribute.innertext == netbios-name
                                    if (childAttr.InnerText == "netbios-name") {
                                        netbiosname = hostChild.InnerText; // get the outside child text;
                                    } else if (childAttr.InnerText == "hostname") {
                                        hostname = hostChild.InnerText; // get the outside child text;
                                    } else if (childAttr.InnerText == "operating-system") {
                                        operatingSystem = hostChild.InnerText; // get the outside child text;
                                    } else if (childAttr.InnerText == "system-type") {
                                        systemType = hostChild.InnerText; // get the outside child text;
                                    } else if (childAttr.InnerText == "Credentialed_Scan") {
                                        bool.TryParse(hostChild.InnerText, out credentialed); // get the outside child text;
                                    } else if (childAttr.InnerText == "host-rdns") {
                                        ipAddress = hostChild.InnerText; // get the outside child text;
                                    }
                                }// for each childAttr in hostChild
                            } // for each hostChild
                        }
                        else if (child.Name == "ReportItem") {
                            // get the report host name
                            // get all ReportItems and their attributes in the tag 
                            colAttributes = child.Attributes;
                            patchData = new NessusPatchData();
                            // set the hostname and other host data for every single record
                            patchData.hostname = hostname;
                            patchData.operatingSystem = operatingSystem;
                            patchData.ipAddress = SanitizeHostname(ipAddress); // if an IP clean up the information octets
                            patchData.systemType = systemType;
                            patchData.credentialed = credentialed;
                            // get all the attributes
                            foreach (XmlAttribute attr in colAttributes) {
                                if (attr.Name == "severity") {
                                    // store the integer
                                    patchData.severity = Convert.ToInt32(attr.Value);
                                } else if (attr.Name == "pluginID") {
                                    patchData.pluginId = attr.Value;
                                } else if (attr.Name == "pluginName") { 
                                    patchData.pluginName = attr.Value;
                                } else if (attr.Name == "pluginFamily") {
                                    patchData.family = attr.Value;
                                }
                            }
                            // get all the child record data we need
                            foreach (XmlElement reportData in child.ChildNodes) {
                                if (reportData.Name == "description")
                                    patchData.description = reportData.InnerText;
                                else if (reportData.Name == "plugin_publication_date")
                                    patchData.publicationDate = reportData.InnerText;
                                else if (reportData.Name == "plugin_type")
                                    patchData.pluginType = reportData.InnerText;
                                else if (reportData.Name == "risk_factor")
                                    patchData.riskFactor = reportData.InnerText;
                                else if (reportData.Name == "synopsis")
                                    patchData.synopsis = reportData.InnerText;

                                if (patchData.family == "Settings" && patchData.pluginName == "Nessus Scan Information") { // get the version of ACAS
                                    if (reportData.Name == "plugin_output") { // parse the data in here
                                        int strPlacement = 0;
                                        strPlacement = reportData.InnerText.IndexOf("Nessus version : ");
                                        if (strPlacement > 0) { // record the version
                                            scanVersion = reportData.InnerText.Substring(strPlacement+17, reportData.InnerText.IndexOf("\n",strPlacement+19)-(strPlacement+17)).Trim();
                                            strPlacement = reportData.InnerText.IndexOf("Plugin feed version : ");
                                            if (strPlacement > 0) // add the plugin feed version to the end of the ACAS version
                                                scanVersion += "." + reportData.InnerText.Substring(strPlacement+22, reportData.InnerText.IndexOf("\n",strPlacement+24)-(strPlacement+22)).Trim();
                                        }
                                    }
                                }
                            }
                            // record the ACAS version for the POA&M export
                            if (!string.IsNullOrEmpty(scanVersion)) patchData.scanVersion = scanVersion;

                            // add the record
                            listPatchData.Add(patchData);
                        }
                    }
                }
            }
            return listPatchData;
        }

        /// <summary>
        /// Called to remove the first two octets from an IP Address if this is an IP
        /// </summary>
        /// <param name="hostname">The hostname or IP of the system</param>
        /// <returns>
        /// The hostname if just a string, the IP address if an IP with xxx.xxx. to start 
        /// the IP range. So the first two octets are hidden from view for security reasons.
        /// </returns>
        public static string SanitizeHostname(string hostname){
            // if this is not an IP, just return the host
            if (hostname.IndexOf(".") <= 0)
                return hostname;
            else {
                System.Net.IPAddress hostAddress;
                if (System.Net.IPAddress.TryParse(hostname.Trim(), out hostAddress)){
                    // this is an IP address so return the last two octets
                    return "xxx.xxx." + hostAddress.GetAddressBytes()[2] + "." + hostAddress.GetAddressBytes()[3];
                }
                else 
                    return hostname;
            }
        }
    }
}