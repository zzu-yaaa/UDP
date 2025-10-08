#include <iostream>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <chrono>
#include <cstring>
#include <cstdint>
#include <cmath>
#include <numeric>
#include <array>
#include <iomanip>

#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")

struct Packet {
    uint32_t Seq = 0;
    uint64_t Timestamp = 0;
    std::array<uint8_t, 244> Content{};

    bool Deserialize(const uint8_t* buf, size_t len) {
        if (len != 256) return false;

        // Big-Endian → Host 변환 (C# Serialize가 네트워크 바이트순서로 보냄)
        Seq = (uint32_t(buf[0]) << 24) |
            (uint32_t(buf[1]) << 16) |
            (uint32_t(buf[2]) << 8) |
            (uint32_t(buf[3]));

        Timestamp = (uint64_t(buf[4]) << 56) |
            (uint64_t(buf[5]) << 48) |
            (uint64_t(buf[6]) << 40) |
            (uint64_t(buf[7]) << 32) |
            (uint64_t(buf[8]) << 24) |
            (uint64_t(buf[9]) << 16) |
            (uint64_t(buf[10]) << 8) |
            (uint64_t(buf[11]));

        std::memcpy(Content.data(), buf + 12, 244);
        return true;
    }
};

static const int   serverPort = 8888;
static std::unordered_map<int, int> dupMap;
static std::unordered_map<int, long long> latencyMap;

static long long GetPercentile(const std::vector<long long>& sorted, double percentile) {
    if (sorted.empty()) return 0;
    int N = (int)sorted.size();
    double rank = (percentile / 100.0) * (N - 1);
    int low = (int)std::floor(rank);
    int high = (int)std::ceil(rank);
    if (low == high) return sorted[low];
    double weight = rank - low;
    return (long long)(sorted[low] + weight * (sorted[high] - sorted[low]));
}

static void printResult() {
    std::cout << "\n=== 결과 통계 ===\n";
    if (dupMap.empty()) {
        std::cout << "수집된 패킷 없음\n";
        return;
    }

    int totalRecv = 0;
    for (auto& kv : dupMap) totalRecv += kv.second;
    int uniqRecv = (int)dupMap.size();
    int dupCount = totalRecv - uniqRecv;

    int minSeq = dupMap.begin()->first;
    int maxSeq = dupMap.begin()->first;
    for (auto& kv : dupMap) {
        if (kv.first < minSeq) minSeq = kv.first;
        if (kv.first > maxSeq) maxSeq = kv.first;
    }
    int expected = maxSeq - minSeq + 1;
    int loss = expected - uniqRecv;
    double lossRate = expected > 0 ? (loss * 100.0 / expected) : 0.0;

    std::cout << "총 수신 패킷(중복 포함): " << totalRecv << "\n";
    std::cout << "고유 패킷 수: " << uniqRecv << ", 중복 패킷 수: " << dupCount << "\n";
    std::cout << "손실 추정: " << loss << " / " << expected
        << " (" << std::fixed << std::setprecision(2) << lossRate << "%)\n";

    if (!latencyMap.empty()) {
        std::vector<long long> lats; lats.reserve(latencyMap.size());
        for (auto& kv : latencyMap) lats.push_back(kv.second);
        std::sort(lats.begin(), lats.end());

        long long minv = lats.front();
        long long maxv = lats.back();
        double avg = std::accumulate(lats.begin(), lats.end(), 0.0) / (double)lats.size();

        long long p50 = GetPercentile(lats, 50);
        long long p90 = GetPercentile(lats, 90);
        long long p95 = GetPercentile(lats, 95);
        long long p99 = GetPercentile(lats, 99);

        std::cout << "지연(ms): min=" << minv
            << ", avg=" << std::fixed << std::setprecision(2) << avg
            << ", p50=" << p50
            << ", p90=" << p90
            << ", p95=" << p95
            << ", p99=" << p99
            << ", max=" << maxv << "\n";
    }
}

static void StartListener() {
    std::cout << "C++ 데이터 수신 시작\n";

	// winsock 초기화 (windows에서 socker 사용시 필요)
    WSADATA wsaData;
    int wret = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (wret != 0) {
        std::cerr << "WSAStartup failed: " << wret << "\n";
        return;
    }

	// udp 소켓 생성
    // AF_INET : IPv4, SOCK_DGRAM: 데이터그램(UDP) 소켓, IPPROTO_UDP : UDP 프로토콜 사용
    SOCKET sockfd = ::socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sockfd == INVALID_SOCKET) {
        std::cerr << "socket error: " << WSAGetLastError() << "\n";
        WSACleanup();
        return;
    }

	// 바인딩할 주소 설정 (INADDR_ANY: 모든 인터페이스, serverPort: 지정 포트)
    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons((u_short)serverPort);

    // 바인딩
    if (::bind(sockfd, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        std::cerr << "bind error: " << WSAGetLastError() << "\n";
        closesocket(sockfd);
        WSACleanup();
        return;
    }

    auto start = std::chrono::steady_clock::now();
    long long lastReceiveTimeMs = 0;

    try {
        while (true) {
            std::cout << "Waiting for broadcast\n";
            uint8_t buf[256];
            sockaddr_in src{};
            int srclen = sizeof(src);

            int n = ::recvfrom(sockfd, (char*)buf, (int)sizeof(buf), 0,
                (sockaddr*)&src, &srclen);
            if (n == SOCKET_ERROR) {
                std::cerr << "recvfrom error: " << WSAGetLastError() << "\n";
                break;
            }
            if (n != 256) {
                break;
            }

            char srcip[64]{};
            inet_ntop(AF_INET, &src.sin_addr, srcip, (socklen_t)sizeof(srcip));
            std::cout << "Received broadcast from " << srcip
                << ":" << ntohs(src.sin_port) << " :\n";

            Packet packet;
            if (!packet.Deserialize(buf, (size_t)n)) {
                std::cout << "  Deserialize failed\n";
                continue;
            }

            std::cout << "  Seq: " << packet.Seq
                << ", Timestamp: " << packet.Timestamp << "\n";

            int key = (int)packet.Seq;
            auto it = dupMap.find(key);
            if (it == dupMap.end()) dupMap[key] = 1;
            else it->second += 1;

            // 현재 UTC epoch ms - 전송 타임스탬프
            auto nowMs = (long long)std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count();
            long long lat = nowMs - (long long)packet.Timestamp;
            latencyMap[key] = lat;

        }
    }
    catch (...) {
        std::cerr << "Exception occurred in receive loop\n";
    }

    closesocket(sockfd);
    WSACleanup();

    auto end = std::chrono::steady_clock::now();
    auto elapsedMs = std::chrono::duration_cast<std::chrono::milliseconds>(end - start).count();
    std::cout << "데이터 수신 종료, " << (elapsedMs / 1000) << " seconds\n";
}

int main() {
    StartListener();
    printResult();
    return 0;
}
