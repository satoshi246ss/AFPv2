// C# 7.3 / .NET Framework 4.8
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace MeteorDetection
{
    /// <summary>UDPメッセージ種別</summary>
    public enum MeteorUdpMessageType : byte
    {
        /// <summary>流星候補確定＝追尾カメラへの導入指令</summary>
        Start = 0,
        /// <summary>追尾中の位置・速度の途中経過</summary>
        Update = 1,
        /// <summary>検知終了</summary>
        End = 2
    }

    /// <summary>追尾カメラへ送る1メッセージ分のデータ。</summary>
    public sealed class MeteorUdpMessage
    {
        public MeteorUdpMessageType Type;
        public int TrackId;
        public double UnixTimeSeconds;   // UTC, 1970-01-01からの秒数
        public double AzDeg;             // 方位角 [deg] (北0, 東90)
        public double AltDeg;            // 高度角 [deg]
        public double VelAzDegPerSec;    // 方位角の角速度 [deg/s]
        public double VelAltDegPerSec;   // 高度角の角速度 [deg/s]
        public double PixelX;            // 画像上のx [px] (参考情報)
        public double PixelY;            // 画像上のy [px]
        public double VelPixelXPerSec;   // 画像上の速度 x [px/s]
        public double VelPixelYPerSec;   // 画像上の速度 y [px/s]
        public double PeakValue;         // 明るさの目安（背景差分ピーク値）
    }

    /// <summary>
    /// 流星追尾カメラへUDPで指令を送信する。プロトコルは固定長バイナリ（85バイト）で、
    /// 送信頻度は最大30Hz程度のため負荷は無視できるレベル。
    /// バイト列レイアウト (リトルエンディアン):
    ///   [0]      byte   MessageType (0=Start,1=Update,2=End)
    ///   [1:5)    int32  TrackId
    ///   [5:13)   double UnixTimeSeconds
    ///   [13:21)  double AzDeg
    ///   [21:29)  double AltDeg
    ///   [29:37)  double VelAzDegPerSec
    ///   [37:45)  double VelAltDegPerSec
    ///   [45:53)  double PixelX
    ///   [53:61)  double PixelY
    ///   [61:69)  double VelPixelXPerSec
    ///   [69:77)  double VelPixelYPerSec
    ///   [77:85)  double PeakValue
    /// </summary>
    public sealed class MeteorUdpSender : IDisposable
    {
        public const int PacketSize = 85;

        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;

        public MeteorUdpSender(string host, int port)
        {
            IPAddress addr;
            if (!IPAddress.TryParse(host, out addr))
                addr = Dns.GetHostAddresses(host).First(a => a.AddressFamily == AddressFamily.InterNetwork);

            _client = new UdpClient();
            _endpoint = new IPEndPoint(addr, port);
        }

        public void Send(MeteorUdpMessage m)
        {
            using (var ms = new MemoryStream(PacketSize))
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)m.Type);
                w.Write(m.TrackId);
                w.Write(m.UnixTimeSeconds);
                w.Write(m.AzDeg);
                w.Write(m.AltDeg);
                w.Write(m.VelAzDegPerSec);
                w.Write(m.VelAltDegPerSec);
                w.Write(m.PixelX);
                w.Write(m.PixelY);
                w.Write(m.VelPixelXPerSec);
                w.Write(m.VelPixelYPerSec);
                w.Write(m.PeakValue);

                var bytes = ms.ToArray();
                _client.Send(bytes, bytes.Length, _endpoint);
            }
        }

        public static double ToUnixSeconds(DateTime t) =>
            (t.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public void Dispose() => _client.Close();
    }

    /// <summary>
    /// 受信側（追尾カメラ）向けの参考パーサ。MeteorUdpSenderと対になるバイト列を復元する。
    /// </summary>
    public static class MeteorUdpParser
    {
        public static MeteorUdpMessage Parse(byte[] data)
        {
            if (data == null || data.Length < MeteorUdpSender.PacketSize)
                throw new ArgumentException("パケット長が不正です。");

            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                return new MeteorUdpMessage
                {
                    Type = (MeteorUdpMessageType)r.ReadByte(),
                    TrackId = r.ReadInt32(),
                    UnixTimeSeconds = r.ReadDouble(),
                    AzDeg = r.ReadDouble(),
                    AltDeg = r.ReadDouble(),
                    VelAzDegPerSec = r.ReadDouble(),
                    VelAltDegPerSec = r.ReadDouble(),
                    PixelX = r.ReadDouble(),
                    PixelY = r.ReadDouble(),
                    VelPixelXPerSec = r.ReadDouble(),
                    VelPixelYPerSec = r.ReadDouble(),
                    PeakValue = r.ReadDouble()
                };
            }
        }
    }
}
