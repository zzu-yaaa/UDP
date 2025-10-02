using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class dataTx
{
    const string serverIp = "127.0.0.1";
    const int serverPort = 9999;

    static Dictionary<int, int> dup = new Dictionary<int, int>();
    static Dictionary<int, long> latency = new Dictionary<int, long>();

    static void Main(string[] args)
    {
        Console.WriteLine("데이터 수신 시작");

        // (1) UdpClient 객체 성성 및 연결
        UdpClient udpClient = new UdpClient();
        udpClient.Connect(serverIp, serverPort);

        var sw = Stopwatch.StartNew();
        long lastReceiveTime = sw.ElapsedMilliseconds; //현재 스톱워치 틱

        while (true)
        {
            IPEndPoint epRemote = new IPEndPoint(IPAddress.Any, 0);
            byte[] bytes = udpClient.Receive(ref epRemote);

            Console.WriteLine("[Receive] {0} 로부터 {1} 바이트 수신", epRemote.ToString(), bytes.Length);

            Packet packet = new Packet().Deserialize(bytes);
            Console.WriteLine("  Seq: {0}, Timestamp: {1}", packet.Seq, packet.Timestamp);
            dup.Add((int)packet.Seq, dup.ContainsKey((int)packet.Seq) ? dup[(int)packet.Seq] + 1 : 1);
            latency.Add((int)packet.Seq, (long)(sw.ElapsedMilliseconds - (long)packet.Timestamp));

            lastReceiveTime = sw.ElapsedMilliseconds;
        }

        sw.Stop();

        // (3) 자원 해제
        udpClient.Close();
        Console.WriteLine("데이터 송신 종료, {0} seconds", sw.ElapsedMilliseconds / 1000);
    }
}
