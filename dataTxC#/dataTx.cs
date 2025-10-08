using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class dataTx
{
    const int Hz = 500;
    const int DurationSec = 600;
    const int TotalPackets = Hz * DurationSec;

    // 데이터를 보낼 서버의 IP 주소와 포트 번호
    const string serverIp = "127.0.0.1";
    const int serverPort = 8888;

    static void Main(string[] args)
    {
        Console.WriteLine("데이터 송신 시작");

        // UDP 소켓 생성
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        s.EnableBroadcast = true;

        // 서버의 IP와 포트 정보를 담은 객체 생성 (설정)
        IPAddress broadcast = IPAddress.Parse(serverIp);
        IPEndPoint ep = new IPEndPoint(broadcast, serverPort);

        var sw = Stopwatch.StartNew();
        long nextDeadline = sw.ElapsedTicks; //현재 스톱워치 틱
        long freq = Stopwatch.Frequency; //1s 당 틱 수
        double ticksPerMs = Stopwatch.Frequency / 1000.0; // 1ms 당 틱 수
        long intervalTicks = (long)(2 * ticksPerMs); // 2ms 간격

        // 데이터 송신
        for (int i = 0; i < TotalPackets; i++)
        {
            Packet packet = new Packet
            {
                Seq = (UInt32)i,
                Timestamp = (UInt64)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            byte[] datagram = packet.Serialize();
            s.SendTo(datagram, ep);

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


        string msg = "END";
        byte[] endMsg = Encoding.UTF8.GetBytes(msg);
        s.SendTo(endMsg, ep);
        s.Close();

        Console.WriteLine("데이터 송신 종료, {0} seconds", sw.ElapsedMilliseconds / 1000);
    }
}
