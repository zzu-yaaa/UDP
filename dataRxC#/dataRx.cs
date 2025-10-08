using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class dataRx
{
    const string serverIp = "127.0.0.1";
    const int serverPort = 8888;

    static Dictionary<int, int> dup = new Dictionary<int, int>();
    static Dictionary<int, long> latency = new Dictionary<int, long>();

    private static void StartListener()
    {
        Console.WriteLine("C# 데이터 수신 시작");

        // (1) UdpClient 객체 성성 및 연결
        UdpClient udpClient = new UdpClient(serverPort);
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, serverPort);

        var sw = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                Console.WriteLine("Waiting for broadcast");
                byte[] bytes = udpClient.Receive(ref groupEP);
                if (bytes.Length != 256)
                {
                    Console.WriteLine("Invalid packet size, stopping listener.");
                    break;
                }

                Console.WriteLine($"Received broadcast from {groupEP} :");
                Packet packet = new Packet().Deserialize(bytes);
                Console.WriteLine("  Seq: {0}, Timestamp: {1}", packet.Seq, packet.Timestamp);
                dup.Add((int)packet.Seq, dup.ContainsKey((int)packet.Seq) ? dup[(int)packet.Seq] + 1 : 1);
                latency.Add((int)packet.Seq, (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)packet.Timestamp));
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            udpClient.Close();
            sw.Stop();
            Console.WriteLine("데이터 수신 종료, {0} seconds", sw.ElapsedMilliseconds / 1000);
        }
    }

    private static void printResult()
    {
        Console.WriteLine("\n=== 결과 통계 ===");

        if (dup.Count == 0)
        {
            Console.WriteLine("수집된 패킷 없음");
            return;
        }

        // 총 수신/중복
        int totalRecv = dup.Values.Sum();
        int uniqRecv = dup.Count;
        int dupCount = totalRecv - uniqRecv;

        // 시퀀스 범위 기반으로 손실률 추정 (패킷이 연속적으로 온다고 가정)
        int minSeq = dup.Keys.Min();
        int maxSeq = dup.Keys.Max();
        int expected = maxSeq - minSeq + 1;
        int loss = expected - uniqRecv;
        double lossRate = expected > 0 ? (loss * 100.0 / expected) : 0.0;

        Console.WriteLine($"총 수신 패킷(중복 포함): {totalRecv}");
        Console.WriteLine($"고유 패킷 수: {uniqRecv}, 중복 패킷 수: {dupCount}");
        Console.WriteLine($"손실 추정: {loss} / {expected} ({lossRate:F2}%)");

        if (latency.Count > 0)
        {
            var lats = latency.Values.ToList();
            lats.Sort();

            long min = lats.First();
            long max = lats.Last();
            double avg = lats.Average();

            long p50 = GetPercentile(lats, 50);
            long p90 = GetPercentile(lats, 90);
            long p95 = GetPercentile(lats, 95);
            long p99 = GetPercentile(lats, 99);

            Console.WriteLine(
                $"지연(ms): min={min}, avg={avg:F2}, " +
                $"p50={p50}, p90={p90}, p95={p95}, p99={p99}, max={max}"
            );
        }
    }

    // 퍼센타일 계산 함수
    static long GetPercentile(List<long> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;

        int N = sorted.Count;
        double rank = (percentile / 100.0) * (N - 1);
        int low = (int)Math.Floor(rank);
        int high = (int)Math.Ceiling(rank);

        if (low == high) return sorted[low];

        double weight = rank - low;
        return (long)(sorted[low] + weight * (sorted[high] - sorted[low]));
    }


    static void Main(string[] args)
    {
        StartListener();
        printResult();
    }
}
