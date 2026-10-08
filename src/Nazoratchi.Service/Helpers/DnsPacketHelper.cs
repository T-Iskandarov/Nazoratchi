using System.Text;
using System.Collections.Generic;

namespace Nazoratchi.Service.Helpers;

/// <summary>
/// Utilities for parsing and building DNS packets.
/// </summary>
public static class DnsPacketHelper
{
    public static string ParseDomainFromQuery(byte[] packet)
    {
        try
        {
            if (packet.Length < 12) return string.Empty;

            var sb = new StringBuilder();
            int pos = 12; // Skip 12-byte header

            while (pos < packet.Length)
            {
                int len = packet[pos];
                if (len == 0) break;

                if (pos > 12) sb.Append('.');
                pos++;
                
                sb.Append(Encoding.ASCII.GetString(packet, pos, len));
                pos += len;
            }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static byte[] BuildBlockedResponse(byte[] queryPacket)
    {
        if (queryPacket.Length < 12) return queryPacket;

        try
        {
            // Find end of QNAME
            int pos = 12;
            while (pos < queryPacket.Length && queryPacket[pos] != 0)
            {
                pos += queryPacket[pos] + 1;
            }
            pos++; // Skip null byte
            pos += 4; // Skip QTYPE and QCLASS

            // Build response
            var response = new List<byte>();
            
            // ID
            response.Add(queryPacket[0]);
            response.Add(queryPacket[1]);
            
            // Flags: Standard response, no error
            response.Add(0x81);
            response.Add(0x80);
            
            // QDCOUNT (1)
            response.Add(0x00);
            response.Add(0x01);
            
            // ANCOUNT (1)
            response.Add(0x00);
            response.Add(0x01);
            
            // NSCOUNT (0)
            response.Add(0x00);
            response.Add(0x00);
            
            // ARCOUNT (0)
            response.Add(0x00);
            response.Add(0x00);

            // Question Section (Copy from query)
            for (int i = 12; i < pos; i++)
            {
                response.Add(queryPacket[i]);
            }

            // Answer Section
            // Name: Pointer to question name (Offset 12)
            response.Add(0xc0);
            response.Add(0x0c);
            
            // Type: A (1)
            response.Add(0x00);
            response.Add(0x01);
            
            // Class: IN (1)
            response.Add(0x00);
            response.Add(0x01);
            
            // TTL: 60
            response.Add(0x00);
            response.Add(0x00);
            response.Add(0x00);
            response.Add(0x3c);
            
            // RDLENGTH: 4 (IPv4 address)
            response.Add(0x00);
            response.Add(0x04);
            
            // RDATA: 0.0.0.0
            response.Add(0);
            response.Add(0);
            response.Add(0);
            response.Add(0);

            return response.ToArray();
        }
        catch
        {
            return queryPacket; // Fallback
        }
    }
}
