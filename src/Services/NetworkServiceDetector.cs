using System;
using System.Collections.Generic;
using System.Net;

namespace AppIntBlockerGUI.Services
{
    public enum NetworkProtocol
    {
        TCP,
        UDP,
        ICMP,
        Unknown
    }

    public enum TrafficDirection
    {
        Incoming,
        Outgoing,
        Local,
        Unknown
    }

    public enum TrafficType
    {
        Unicast,
        Multicast,
        Broadcast,
        Local,
        Unknown
    }

    public class NetworkServiceInfo
    {
        public string ServiceName { get; set; } = "Unknown";
        public string Description { get; set; } = "";
        public ushort Port { get; set; }
        public NetworkProtocol Protocol { get; set; }
        public TrafficDirection Direction { get; set; }
        public TrafficType Type { get; set; }
        public bool IsSystemService { get; set; }
        public bool IsSecuritySensitive { get; set; }
        public string Category { get; set; } = "Unknown";
    }

    public static class NetworkServiceDetector
    {
        private static readonly Dictionary<(ushort Port, NetworkProtocol Protocol), NetworkServiceInfo> ServiceDatabase;

        static NetworkServiceDetector()
        {
            ServiceDatabase = InitializeServiceDatabase();
        }

        public static NetworkServiceInfo AnalyzeConnection(
            IPAddress sourceIp, 
            ushort? sourcePort, 
            IPAddress destIp, 
            ushort? destPort, 
            NetworkProtocol protocol,
            IPAddress[] localAddresses)
        {
            var direction = DetermineTrafficDirection(sourceIp, destIp, sourcePort, destPort, localAddresses);
            var trafficType = DetermineTrafficType(destIp, direction);
            
            // Determine which port to use for service detection based on traffic direction
            ushort? servicePort = null;
            if (direction == TrafficDirection.Outgoing && destPort.HasValue)
            {
                servicePort = destPort; // We're connecting to a service
            }
            else if (direction == TrafficDirection.Incoming && sourcePort.HasValue)
            {
                servicePort = sourcePort; // Service is connecting to us
            }
            else if (destPort.HasValue && IsWellKnownPort(destPort.Value))
            {
                servicePort = destPort; // Well-known port takes precedence
            }
            else if (sourcePort.HasValue && IsWellKnownPort(sourcePort.Value))
            {
                servicePort = sourcePort;
            }
            else
            {
                servicePort = destPort ?? sourcePort; // Default to destination port
            }

            var service = DetectService(servicePort, protocol);
            service.Direction = direction;
            service.Type = trafficType;

            return service;
        }

        private static NetworkServiceInfo DetectService(ushort? port, NetworkProtocol protocol)
        {
            if (!port.HasValue)
            {
                return new NetworkServiceInfo 
                { 
                    ServiceName = protocol == NetworkProtocol.ICMP ? "ICMP" : "Unknown",
                    Protocol = protocol,
                    Category = protocol == NetworkProtocol.ICMP ? "Network" : "Unknown"
                };
            }

            if (ServiceDatabase.TryGetValue((port.Value, protocol), out var service))
            {
                return new NetworkServiceInfo
                {
                    ServiceName = service.ServiceName,
                    Description = service.Description,
                    Port = port.Value,
                    Protocol = protocol,
                    IsSystemService = service.IsSystemService,
                    IsSecuritySensitive = service.IsSecuritySensitive,
                    Category = service.Category
                };
            }

            // Enhanced unknown service classification
            return new NetworkServiceInfo
            {
                ServiceName = GetUnknownServiceName(port.Value, protocol),
                Description = $"Unknown {protocol} service on port {port.Value}",
                Port = port.Value,
                Protocol = protocol,
                Category = CategorizeUnknownPort(port.Value)
            };
        }

        private static TrafficDirection DetermineTrafficDirection(
            IPAddress sourceIp, 
            IPAddress destIp, 
            ushort? sourcePort, 
            ushort? destPort,
            IPAddress[] localAddresses)
        {
            // Check for loopback traffic
            if (IPAddress.IsLoopback(sourceIp) && IPAddress.IsLoopback(destIp))
            {
                if (sourcePort.HasValue && destPort.HasValue)
                {
                    return sourcePort > destPort ? TrafficDirection.Outgoing : TrafficDirection.Incoming;
                }
                return TrafficDirection.Local;
            }

            // Check if source IP is local
            if (IsLocalAddress(sourceIp, localAddresses))
            {
                return TrafficDirection.Outgoing;
            }

            // Check if destination IP is local  
            if (IsLocalAddress(destIp, localAddresses))
            {
                return TrafficDirection.Incoming;
            }

            // Default determination based on well-known ports
            if (destPort.HasValue && IsWellKnownPort(destPort.Value))
            {
                return TrafficDirection.Outgoing; // Connecting to a well-known service
            }

            return TrafficDirection.Unknown;
        }

        private static TrafficType DetermineTrafficType(IPAddress destIp, TrafficDirection direction)
        {
            if (direction == TrafficDirection.Local)
                return TrafficType.Local;

            if (IsMulticastAddress(destIp))
                return TrafficType.Multicast;

            if (IsBroadcastAddress(destIp))
                return TrafficType.Broadcast;

            return TrafficType.Unicast;
        }

        private static bool IsLocalAddress(IPAddress address, IPAddress[] localAddresses)
        {
            if (localAddresses == null) return false;

            foreach (var localAddr in localAddresses)
            {
                if (address.Equals(localAddr))
                    return true;
            }

            // Check for private IP ranges
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
                return (bytes[0] == 10) ||
                       (bytes[0] == 172 && (bytes[1] & 0xF0) == 16) ||
                       (bytes[0] == 192 && bytes[1] == 168) ||
                       address.ToString().StartsWith("169.254."); // Link-local
            }

            return false;
        }

        private static bool IsMulticastAddress(IPAddress address)
        {
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                return (bytes[0] & 0xF0) == 0xE0; // 224.0.0.0/4
            }
            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return address.ToString().StartsWith("ff");
            }

            return false;
        }

        private static bool IsBroadcastAddress(IPAddress address)
        {
            return address.Equals(IPAddress.Broadcast) || 
                   address.ToString().EndsWith(".255");
        }

        private static bool IsWellKnownPort(ushort port)
        {
            return port <= 1023; // IANA well-known ports
        }

        private static string GetUnknownServiceName(ushort port, NetworkProtocol protocol)
        {
            var portRange = port switch
            {
                <= 1023 => "System",
                <= 49151 => "Registered",
                _ => "Dynamic"
            };

            return $"{portRange} {protocol} ({port})";
        }

        private static string CategorizeUnknownPort(ushort port)
        {
            return port switch
            {
                <= 1023 => "System Services",
                <= 5000 => "Network Services", 
                <= 10000 => "Application Services",
                <= 32767 => "User Services",
                _ => "Dynamic Ports"
            };
        }

        private static Dictionary<(ushort Port, NetworkProtocol Protocol), NetworkServiceInfo> InitializeServiceDatabase()
        {
            var db = new Dictionary<(ushort Port, NetworkProtocol Protocol), NetworkServiceInfo>();

            // Core network services (enhanced from Sniffnet's database)
            AddService(db, 21, NetworkProtocol.TCP, "FTP", "File Transfer Protocol", "File Transfer", true, true);
            AddService(db, 22, NetworkProtocol.TCP, "SSH", "Secure Shell", "Remote Access", true, true);
            AddService(db, 23, NetworkProtocol.TCP, "Telnet", "Telnet Protocol", "Remote Access", true, true);
            AddService(db, 25, NetworkProtocol.TCP, "SMTP", "Simple Mail Transfer Protocol", "Email", true, false);
            AddService(db, 53, NetworkProtocol.TCP, "DNS", "Domain Name System", "Network", true, false);
            AddService(db, 53, NetworkProtocol.UDP, "DNS", "Domain Name System", "Network", true, false);
            AddService(db, 67, NetworkProtocol.UDP, "DHCP Server", "Dynamic Host Configuration Protocol", "Network", true, false);
            AddService(db, 68, NetworkProtocol.UDP, "DHCP Client", "Dynamic Host Configuration Protocol", "Network", true, false);
            AddService(db, 69, NetworkProtocol.UDP, "TFTP", "Trivial File Transfer Protocol", "File Transfer", false, false);
            AddService(db, 80, NetworkProtocol.TCP, "HTTP", "Hypertext Transfer Protocol", "Web", false, false);
            AddService(db, 110, NetworkProtocol.TCP, "POP3", "Post Office Protocol v3", "Email", false, false);
            AddService(db, 111, NetworkProtocol.TCP, "RPC", "Remote Procedure Call", "System", true, true);
            AddService(db, 111, NetworkProtocol.UDP, "RPC", "Remote Procedure Call", "System", true, true);
            AddService(db, 113, NetworkProtocol.TCP, "Ident", "Identification Protocol", "Network", false, false);
            AddService(db, 119, NetworkProtocol.TCP, "NNTP", "Network News Transfer Protocol", "News", false, false);
            AddService(db, 123, NetworkProtocol.UDP, "NTP", "Network Time Protocol", "Network", false, false);
            AddService(db, 135, NetworkProtocol.TCP, "RPC Endpoint Mapper", "Windows RPC", "System", true, true);
            AddService(db, 137, NetworkProtocol.UDP, "NetBIOS Name", "NetBIOS Name Service", "Windows", true, false);
            AddService(db, 138, NetworkProtocol.UDP, "NetBIOS Datagram", "NetBIOS Datagram Service", "Windows", true, false);
            AddService(db, 139, NetworkProtocol.TCP, "NetBIOS Session", "NetBIOS Session Service", "Windows", true, false);
            AddService(db, 143, NetworkProtocol.TCP, "IMAP", "Internet Message Access Protocol", "Email", false, false);
            AddService(db, 161, NetworkProtocol.UDP, "SNMP", "Simple Network Management Protocol", "Network", true, true);
            AddService(db, 162, NetworkProtocol.UDP, "SNMP Trap", "SNMP Notifications", "Network", true, true);
            AddService(db, 389, NetworkProtocol.TCP, "LDAP", "Lightweight Directory Access Protocol", "Directory", true, true);
            AddService(db, 443, NetworkProtocol.TCP, "HTTPS", "HTTP over TLS/SSL", "Web", false, false);
            AddService(db, 445, NetworkProtocol.TCP, "SMB", "Server Message Block", "File Sharing", true, true);
            AddService(db, 465, NetworkProtocol.TCP, "SMTPS", "SMTP over SSL", "Email", false, false);
            AddService(db, 514, NetworkProtocol.UDP, "Syslog", "System Logging Protocol", "Logging", true, false);
            AddService(db, 515, NetworkProtocol.TCP, "LPR", "Line Printer Remote", "Printing", false, false);
            AddService(db, 587, NetworkProtocol.TCP, "SMTP Submission", "Email Message Submission", "Email", false, false);
            AddService(db, 631, NetworkProtocol.TCP, "IPP", "Internet Printing Protocol", "Printing", false, false);
            AddService(db, 636, NetworkProtocol.TCP, "LDAPS", "LDAP over SSL", "Directory", true, true);
            AddService(db, 993, NetworkProtocol.TCP, "IMAPS", "IMAP over SSL", "Email", false, false);
            AddService(db, 995, NetworkProtocol.TCP, "POP3S", "POP3 over SSL", "Email", false, false);

            // Windows specific services
            AddService(db, 1433, NetworkProtocol.TCP, "SQL Server", "Microsoft SQL Server", "Database", false, true);
            AddService(db, 1434, NetworkProtocol.UDP, "SQL Server Browser", "SQL Server Browser Service", "Database", false, true);
            AddService(db, 3389, NetworkProtocol.TCP, "RDP", "Remote Desktop Protocol", "Remote Access", true, true);
            AddService(db, 5985, NetworkProtocol.TCP, "WinRM HTTP", "Windows Remote Management", "Remote Access", true, true);
            AddService(db, 5986, NetworkProtocol.TCP, "WinRM HTTPS", "Windows Remote Management over SSL", "Remote Access", true, true);

            // Gaming and multimedia
            AddService(db, 1935, NetworkProtocol.TCP, "RTMP", "Real Time Messaging Protocol", "Streaming", false, false);
            AddService(db, 3478, NetworkProtocol.UDP, "STUN", "Session Traversal Utilities for NAT", "Gaming", false, false);
            AddService(db, 5060, NetworkProtocol.TCP, "SIP", "Session Initiation Protocol", "VoIP", false, false);
            AddService(db, 5060, NetworkProtocol.UDP, "SIP", "Session Initiation Protocol", "VoIP", false, false);

            // Development and tools
            AddService(db, 3000, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);
            AddService(db, 3001, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);
            AddService(db, 4000, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);
            AddService(db, 5000, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);
            AddService(db, 8000, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);
            AddService(db, 8080, NetworkProtocol.TCP, "HTTP Alternate", "Alternative HTTP port", "Web", false, false);
            AddService(db, 8443, NetworkProtocol.TCP, "HTTPS Alternate", "Alternative HTTPS port", "Web", false, false);
            AddService(db, 9000, NetworkProtocol.TCP, "Development Server", "Common development server port", "Development", false, false);

            // Modern web services
            AddService(db, 6379, NetworkProtocol.TCP, "Redis", "Redis Database", "Database", false, false);
            AddService(db, 27017, NetworkProtocol.TCP, "MongoDB", "MongoDB Database", "Database", false, false);
            AddService(db, 5432, NetworkProtocol.TCP, "PostgreSQL", "PostgreSQL Database", "Database", false, false);
            AddService(db, 3306, NetworkProtocol.TCP, "MySQL", "MySQL Database", "Database", false, false);

            return db;
        }

        private static void AddService(
            Dictionary<(ushort Port, NetworkProtocol Protocol), NetworkServiceInfo> db,
            ushort port, 
            NetworkProtocol protocol, 
            string name, 
            string description, 
            string category,
            bool isSystem = false, 
            bool isSecurity = false)
        {
            db[(port, protocol)] = new NetworkServiceInfo
            {
                ServiceName = name,
                Description = description,
                Port = port,
                Protocol = protocol,
                IsSystemService = isSystem,
                IsSecuritySensitive = isSecurity,
                Category = category
            };
        }
    }
} 