﻿using System;
using System.Threading;
using AwesomeSockets.Domain.Sockets;
using AwesomeSockets.Sockets;
using NUnit.Framework;
using Buffer = AwesomeSockets.Buffers.Buffer;

namespace AwesomeSockets.Acceptance.Tests
{
    [TestFixture]
    class TcpSyncTests
    {
        [Test]
        public void TcpSynchronousAcceptanceTest()
        {
            var serverGood = false;
            var serverThread = new Thread(() => {
                ServerThread(x => serverGood = x);
            });

            var clientGood = false;
            var clientThread = new Thread(() => {
                ClientThread(x => clientGood = x);
            });

            clientThread.Start();
            serverThread.Start();

            //Sleep to allow the threads a chance to die
            var serverCompleted = serverThread.Join(5000);
            var clientCompleted = clientThread.Join(5000);

            if (!(serverCompleted && clientCompleted))
            {
                //Politely ask both threads to stop before main thread dies and hard-aborts them
#if NET40
                serverThread.Abort();
                clientThread.Abort();
#endif
                Assert.Fail("The threads never returned in the join");
            }
            else
            {
                Assert.IsTrue(clientGood && serverGood, "The client and server thread should have both been good");
            }
        }

#region Shared Methods
        private void SendTestMessage(ISocket other, Buffer sendBuffer)
        {
            Buffer.ClearBuffer(sendBuffer);
            Buffer.Add(sendBuffer, 10);
            Buffer.Add(sendBuffer, 20.0F);
            Buffer.Add(sendBuffer, 40.0);
            Buffer.Add(sendBuffer, 'A');
            Buffer.Add(sendBuffer, "The quick brown fox jumped over the lazy dog");
            Buffer.Add(sendBuffer, (byte)255);
            Buffer.FinalizeBuffer(sendBuffer);

            var bytesSent = AweSock.SendMessage(other, sendBuffer);
            Console.WriteLine("Sent payload. {0} bytes written.", bytesSent);
        }

#endregion

#region Server Methods
        private void ServerThread(Action<bool> callback)
        {
            var sendBuffer = Buffer.New();
            var recvBuffer = Buffer.New();
            var listenSocket = AweSock.TcpListen(14804);
            var client = AweSock.TcpAccept(listenSocket);

            SendTestMessage(client, sendBuffer);
            callback(ReceiveResponseFromClient(client, recvBuffer));
        }       

        private bool ReceiveResponseFromClient(ISocket client, Buffer recvBuffer)
        {
            var serverExitFlag = false;
            do
            {
                var bytesReceived = AweSock.ReceiveMessage(client, recvBuffer);
                if (bytesReceived.Item1 > 0)
                {
                    if (!ValidateResponse(recvBuffer))
                        return false;
                    serverExitFlag = true;
                }
                else if (bytesReceived.Item1 == 0)
                    return false;

                //Thread.Sleep(1000);
            }
            while (!serverExitFlag);
            return true;
        }

        private bool ValidateResponse(Buffer receiveBuffer)
        {
            const float tolerance = 0.00001F;

            return Buffer.Get<int>(receiveBuffer) == 10 &&
                   Math.Abs(Buffer.Get<float>(receiveBuffer) - 20.0F) < tolerance &&
                   Math.Abs(Buffer.Get<double>(receiveBuffer) - 40.0) < tolerance &&
                   Buffer.Get<char>(receiveBuffer) == 'A' &&
                   Buffer.Get<string>(receiveBuffer) == "The quick brown fox jumped over the lazy dog" &&
                   Buffer.Get<byte>(receiveBuffer) == 255;
        }
#endregion

#region Client Methods
        public void ClientThread(Action<bool> callback)
        {
            var sendBuffer = Buffer.New();
            var recvBuffer = Buffer.New();

            var server = AweSock.TcpConnect("127.0.0.1", 14804);

            ReceiveMessageFromServer(server, recvBuffer);
            SendTestMessage(server, sendBuffer);
            callback(true);
        }

        private void ReceiveMessageFromServer(ISocket server, Buffer recvBuffer)
        {
            var clientExitFlag = false;
            do
            {
                var bytesReceived = AweSock.ReceiveMessage(server, recvBuffer);
                if (bytesReceived.Item1 > 0)
                    clientExitFlag = true;
                else if (bytesReceived.Item1 == 0)
                    return;

                Thread.Sleep(1000);
            }
            while (!clientExitFlag);
        }
#endregion
    }
}
