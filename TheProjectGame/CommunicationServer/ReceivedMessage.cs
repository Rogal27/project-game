﻿using System.Net.Sockets;

namespace CommunicationServer
{
    internal class ReceivedMessage
    {
        internal Socket SenderSocket { get; private set; }
        internal string SerializedMessage { get; private set; }

        internal ReceivedMessage(Socket senderSocket, string serializedMessage)
        {
            SenderSocket = senderSocket; 
            SerializedMessage = serializedMessage;
        }
    }
}
