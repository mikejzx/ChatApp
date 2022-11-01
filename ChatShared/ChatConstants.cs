using System.Net;
using System.Text;

namespace Mikejzx.ChatShared
{
    public static class ChatConstants
    {
        // Port for main TCP communications
        public static readonly int ServerPort = 19000;

        // Multicast group IP.
        public static readonly IPAddress MulticastIP = IPAddress.Parse("224.168.9.55");

        // Port for multicasting.
        public static readonly int MulticastPort = 19502;

        // String at beginning of multicast message for identification.
        public static readonly byte[] MulticastMagic = Encoding.ASCII.GetBytes("LanChatMcastMsg_");
    };
}