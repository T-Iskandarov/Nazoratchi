using System.Text;

namespace Nazoratchi.Service.Helpers;

/// <summary>
/// Robust utilities for parsing and building RFC 1035 compliant DNS packets.
/// Protects against buffer overruns, truncated queries, and supports both A and AAAA types.
/// </summary>
public static class DnsPacketHelper
{
    public static string ParseDomainFromQuery(byte[] packet)
    {
        try
        {
            if (packet == null || packet.Length < 12) return string.Empty;

            var sb = new StringBuilder();
            int pos = 12; // Skip 12-byte header

            while (pos < packet.Length)
            {
                int len = packet[pos];
                if (len == 0) break;

                // Handle DNS compression pointer (0xC0) or invalid length
                if ((len & 0xC0) == 0xC0 || len > 63)
                {
                    break;
                }

                pos++;
                if (pos + len > packet.Length)
                {
                    return string.Empty; // Truncated label
                }

                if (sb.Length > 0) sb.Append('.');
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

    /// <summary>
    /// Builds a synthetic blocked DNS response.
    /// Returns 0.0.0.0 for Type A (IPv4) queries or NXDOMAIN (RCODE 3) for other query types.
    /// </summary>
    public static byte[] BuildBlockedResponse(byte[] queryPacket)
    {
        if (queryPacket == null || queryPacket.Length < 12) return queryPacket ?? Array.Empty<byte>();

        try
        {
            // Find end of QNAME
            int pos = 12;
            while (pos < queryPacket.Length && queryPacket[pos] != 0)
            {
                int labelLen = queryPacket[pos];
                if ((labelLen & 0xC0) == 0xC0)
                {
                    pos += 2; // Pointer
                    break;
                }
                pos += labelLen + 1;
            }

            if (pos < queryPacket.Length && queryPacket[pos] == 0)
            {
                pos++; // Skip terminating null byte
            }

            // Must have at least 4 bytes for QTYPE (2) and QCLASS (2)
            if (pos + 4 > queryPacket.Length)
            {
                return queryPacket;
            }

            ushort qtype = (ushort)((queryPacket[pos] << 8) | queryPacket[pos + 1]);
            pos += 4; // Skip QTYPE and QCLASS

            int questionLength = pos;

            // If QTYPE is A (IPv4 = 1), return synthetic A record 0.0.0.0
            if (qtype == 1)
            {
                var response = new List<byte>(questionLength + 16);

                // ID
                response.Add(queryPacket[0]);
                response.Add(queryPacket[1]);

                // Flags: Standard response, No error (0x8180)
                response.Add(0x81);
                response.Add(0x80);

                // QDCOUNT (1)
                response.Add(0x00);
                response.Add(0x01);

                // ANCOUNT (1)
                response.Add(0x00);
                response.Add(0x01);

                // NSCOUNT (0), ARCOUNT (0)
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);

                // Question section (copy verbatim)
                for (int i = 12; i < questionLength; i++)
                {
                    response.Add(queryPacket[i]);
                }

                // Answer section
                // Name pointer to offset 12
                response.Add(0xC0);
                response.Add(0x0C);

                // Type: A (1), Class: IN (1)
                response.Add(0x00);
                response.Add(0x01);
                response.Add(0x00);
                response.Add(0x01);

                // TTL: 60 seconds
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x3C);

                // RDLENGTH: 4
                response.Add(0x00);
                response.Add(0x04);

                // RDATA: 0.0.0.0
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);

                return response.ToArray();
            }
            else
            {
                // For non-A queries (e.g. AAAA, HTTPS, TXT), return RFC-standard NXDOMAIN (RCODE 3)
                var response = new List<byte>(questionLength);

                // ID
                response.Add(queryPacket[0]);
                response.Add(queryPacket[1]);

                // Flags: Response, Authoritative, Name Error / NXDOMAIN (0x8183)
                response.Add(0x81);
                response.Add(0x83);

                // QDCOUNT (1), ANCOUNT (0), NSCOUNT (0), ARCOUNT (0)
                response.Add(0x00);
                response.Add(0x01);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);
                response.Add(0x00);

                // Question section
                for (int i = 12; i < questionLength; i++)
                {
                    response.Add(queryPacket[i]);
                }

                return response.ToArray();
            }
        }
        catch
        {
            return queryPacket;
        }
    }
}
