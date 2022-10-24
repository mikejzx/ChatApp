using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mikejzx.ChatClient
{
    // A client we can chat with.
    public class ChatRecipient
    {
        // Nickname of the client.
        public string nickname;

        // Whether the client is in the server currently.
        public bool isJoined;

        public ChatRecipient(string nickname, bool joined)
        {
            this.nickname = nickname;
            this.isJoined = joined;
        }
    }
}
