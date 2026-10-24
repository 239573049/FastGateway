using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace FastGateway.Risk;

/// <summary>
///     自研人机验证（工作量证明）：浏览器寻找 n，使 SHA-256(challenge + ":" + n) 的前 difficulty 位为 0，
///     服务端只需计算一次哈希即可验证。它拦得住不执行 JS 的通用爆破脚本，并抬高每次尝试的成本，
///     但对使用原生代码或 GPU 的定制攻击者成本很低，因此只用于中风险档，并且必须配合 clearance 额度。
/// </summary>
public static class ProofOfWork
{
    public const int MinDifficulty = 8;
    public const int MaxDifficulty = 26;

    private const int MaxSolutionLength = 16;

    public static bool Verify(string challenge, string? solution, int difficulty)
    {
        if (difficulty is < MinDifficulty or > MaxDifficulty ||
            string.IsNullOrEmpty(solution) || solution.Length > MaxSolutionLength ||
            !solution.All(char.IsAsciiDigit))
            return false;

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(challenge + ":" + solution), hash);
        return LeadingZeroBits(hash) >= difficulty;
    }

    public static int LeadingZeroBits(ReadOnlySpan<byte> hash)
    {
        var bits = 0;
        foreach (var value in hash)
        {
            if (value != 0) return bits + BitOperations.LeadingZeroCount((uint)value) - 24;
            bits += 8;
        }

        return bits;
    }

    /// <summary>
    ///     浏览器端求解器（Web Worker 脚本），网关验证页内联、管理后台登录页按需下载，保持唯一实现。
    ///     challenge 与 n 的总长度远小于 55 字节，因此只实现单个 SHA-256 分组。
    /// </summary>
    public const string WorkerScript = """
        "use strict";
        const K = new Uint32Array([
          0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
          0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
          0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
          0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
          0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
          0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
          0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
          0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        ]);
        const rotr = (x, n) => (x >>> n) | (x << (32 - n));

        self.onmessage = (event) => {
          const challenge = String(event.data.challenge);
          const difficulty = Number(event.data.difficulty);
          const prefix = new TextEncoder().encode(challenge + ":");
          if (prefix.length > 39 || !(difficulty >= 1 && difficulty <= 32)) {
            self.postMessage({ error: "invalid challenge" });
            return;
          }

          const block = new Uint8Array(64);
          const view = new DataView(block.buffer);
          const w = new Uint32Array(64);
          block.set(prefix);

          for (let n = 0; ; n++) {
            const digits = String(n);
            let length = prefix.length;
            for (let i = 0; i < digits.length; i++) block[length++] = digits.charCodeAt(i);
            block[length] = 0x80;
            block.fill(0, length + 1, 60);
            view.setUint32(60, length * 8);

            for (let i = 0; i < 16; i++) w[i] = view.getUint32(i * 4);
            for (let i = 16; i < 64; i++) {
              const a = w[i - 15], b = w[i - 2];
              const s0 = rotr(a, 7) ^ rotr(a, 18) ^ (a >>> 3);
              const s1 = rotr(b, 17) ^ rotr(b, 19) ^ (b >>> 10);
              w[i] = (w[i - 16] + s0 + w[i - 7] + s1) | 0;
            }

            let a = 0x6a09e667, b = 0xbb67ae85, c = 0x3c6ef372, d = 0xa54ff53a;
            let e = 0x510e527f, f = 0x9b05688c, g = 0x1f83d9ab, h = 0x5be0cd19;
            for (let i = 0; i < 64; i++) {
              const t1 = (h + (rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25)) + ((e & f) ^ (~e & g)) + K[i] + w[i]) | 0;
              const t2 = ((rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22)) + ((a & b) ^ (a & c) ^ (b & c))) | 0;
              h = g; g = f; f = e; e = (d + t1) | 0;
              d = c; c = b; b = a; a = (t1 + t2) | 0;
            }

            if (Math.clz32((a + 0x6a09e667) | 0) >= difficulty) {
              self.postMessage({ solution: digits, attempts: n + 1 });
              return;
            }
            if ((n & 0x3ffff) === 0x3ffff) self.postMessage({ attempts: n + 1 });
          }
        };
        """;
}
