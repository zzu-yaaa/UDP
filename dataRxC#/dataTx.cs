using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class dataTx
{
    const int PacketSize = 256;
    const int Hz = 500;                  // 2ms
    const int DurationSec = 60;          // 60s
    const int TotalPackets = Hz * DurationSec;
    
    const string clientIp = "127.0.0.1";
    const int clientPort = 9999;

    static void Main(string[] args)
    {
        Console.WriteLine("데이터 수신 시작");

        // (1) UdpClient 객체 성성 및 연결
        UdpClient udpClient = new UdpClient();
        udpClient.Connect(clientIp, clientPort);

        var sw = Stopwatch.StartNew();
        long nextDeadline = sw.ElapsedTicks; //현재 스톱워치 틱
        long freq = Stopwatch.Frequency; //1s 당 틱 수
        double ticksPerMs = Stopwatch.Frequency / 1000.0; // 1ms 당 틱 수
        long intervalTicks = (long)(2 * ticksPerMs); // 2ms 간격

        // (2) 데이터 송신
        for (int i = 0; i < TotalPackets; i++)
        {
            Packet packet = new Packet
            {
                Seq = (UInt32)i,
                Timestamp = (UInt64)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            byte[] datagram = packet.Serialize();
            udpClient.Send(datagram, PacketSize);

            Console.WriteLine("[Send] {0}:{1} 바이트 전송", i, datagram.Length);

            nextDeadline += intervalTicks; // 다음 목표 시각 갱신 (2ms 후)

            // 목표 시각까지 대기
            while (sw.ElapsedTicks < nextDeadline)
            {
                long remain = nextDeadline - sw.ElapsedTicks;

                if (remain > freq / 1000)
                    Thread.Sleep(0);  // 1ms 이상 남으면 OS에 양보
                else
                    Thread.SpinWait(100); // 의미 없는 연산을 하며 현재 스레드에서 대기
            }
        }

        sw.Stop();

        // (3) 자원 해제
        udpClient.Close();

        Console.WriteLine("데이터 송신 종료, {0} seconds", sw.ElapsedMilliseconds / 1000);
    }
}
