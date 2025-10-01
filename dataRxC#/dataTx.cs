using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class dataTx
{
    static void Main(string[] args)
    {

        Console.WriteLine("데이터 수신 시작");
        Stopwatch sw = new Stopwatch();

        int duration = 60; // seconds
        int totalPackets = duration * 500; // 500 packets per second
        string clientIp = "127.0.0.1";
        int clientPort = 9999;


        // (1) UdpClient 객체 성성
        UdpClient udpClient = new UdpClient();

        sw.Start();

        // (2) 데이터 송신
        for (int i = 0; i < totalPackets; i++)
        {
            Packet packet = new Packet();
            packet.Seq = (UInt32)i;
            packet.Timestamp = (UInt64)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            byte[] datagram = packet.Serialize();
            udpClient.Send(datagram, datagram.Length, clientIp, clientPort);

            Console.WriteLine("[Send] {0}:{1} 바이트 전송", i, datagram.Length);

            Thread.Sleep(2);
        }

        sw.Stop();

        // (3) 자원 해제
        udpClient.Close();

        Console.WriteLine("데이터 송신 종료, {0} seconds", sw.ElapsedMilliseconds / 1000);
    }
}
