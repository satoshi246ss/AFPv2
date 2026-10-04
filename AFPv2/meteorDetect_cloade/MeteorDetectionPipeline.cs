// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
// FisheyeCameraModel (AllSkyCamera 名前空間) を同一プロジェクトに含めると
// 画素座標から方位角・高度角への変換を自動で行う。
using System;
using OpenCvSharp;
using AllSkyCamera;

namespace MeteorDetection
{
    /// <summary>
    /// 検出(MeteorFrameDetector) → 追跡(TrackManager) → UDP通知(MeteorUdpSender)
    /// → 保存(DetectionRecorder) を1本のパイプラインとして統合するクラス。
    /// 呼び出し側はフレームが来るたびに ProcessFrame を呼ぶだけでよい。
    /// </summary>
    public sealed class MeteorDetectionPipeline : IDisposable
    {
        public MeteorFrameDetector Detector { get; }
        public TrackManager Tracker { get; }
        public DetectionRecorder Recorder { get; }
        public MeteorUdpSender UdpSender { get; }

        /// <summary>設定していれば画素座標を方位角・高度角に変換して送信・記録する。</summary>
        public FisheyeCameraModel CameraModel { get; set; }

        /// <summary>true: 確定後の毎フレーム画像を保存する。falseなら開始・終了時のみ。</summary>
        public bool SaveEveryFrame { get; set; } = true;

        /// <summary>保存クロップの中心からの片側マージン [px]</summary>
        public int CropMarginPx { get; set; } = 80;

        public MeteorDetectionPipeline(MeteorFrameDetector detector, TrackManager tracker,
            DetectionRecorder recorder, MeteorUdpSender udpSender, FisheyeCameraModel cameraModel = null)
        {
            Detector = detector ?? throw new ArgumentNullException(nameof(detector));
            Tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            Recorder = recorder;
            UdpSender = udpSender;
            CameraModel = cameraModel;

            Tracker.TrackConfirmed += tr => Emit(tr, MeteorUdpMessageType.Start, saveFrame: true);
            Tracker.TrackUpdated += tr => Emit(tr, MeteorUdpMessageType.Update, saveFrame: SaveEveryFrame);
            Tracker.TrackEnded += tr =>
            {
                Emit(tr, MeteorUdpMessageType.End, saveFrame: true);
                Recorder?.CloseTrack(tr.Id);
            };
        }

        private Mat _lastFrame;

        /// <summary>1フレーム分の処理を行う（撮像スレッドまたは処理スレッドから呼ぶ）。</summary>
        public void ProcessFrame(Mat frameGray8u, DateTime timestampUtc)
        {
            _lastFrame = frameGray8u;
            var detections = Detector.Process(frameGray8u);
            Tracker.Step(detections, timestampUtc);
        }

        private void Emit(MeteorTrack tr, MeteorUdpMessageType type, bool saveFrame)
        {
            var pos = tr.Position;
            var vel = tr.VelocityPxPerSec;

            double az = 0, alt = 0, velAz = 0, velAlt = 0;
            if (CameraModel != null && CameraModel.PixelToHorizontal(pos.X, pos.Y, out az, out alt))
            {
                // 角速度は微小時間差分で近似する
                const double dt = 0.05;
                if (CameraModel.PixelToHorizontal(pos.X + vel.X * dt, pos.Y + vel.Y * dt, out double az2, out double alt2))
                {
                    velAz = NormalizeDeltaDeg(az2 - az) / dt;
                    velAlt = (alt2 - alt) / dt;
                }
            }

            UdpSender?.Send(new MeteorUdpMessage
            {
                Type = type,
                TrackId = tr.Id,
                UnixTimeSeconds = MeteorUdpSender.ToUnixSeconds(tr.LastSeen),
                AzDeg = az,
                AltDeg = alt,
                VelAzDegPerSec = velAz,
                VelAltDegPerSec = velAlt,
                PixelX = pos.X,
                PixelY = pos.Y,
                VelPixelXPerSec = vel.X,
                VelPixelYPerSec = vel.Y,
                PeakValue = tr.PeakValue
            });

            if (Recorder != null)
            {
                Recorder.LogDetection(tr, tr.LastSeen, pos, az, alt);

                if (saveFrame && _lastFrame != null)
                {
                    var crop = new Rect((int)pos.X - CropMarginPx, (int)pos.Y - CropMarginPx,
                        CropMarginPx * 2, CropMarginPx * 2);
                    Recorder.SaveFrame(tr.Id, tr.LastSeen, _lastFrame, crop);
                }
            }
        }

        private static double NormalizeDeltaDeg(double d)
        {
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return d;
        }

        public void Dispose()
        {
            UdpSender?.Dispose();
            Recorder?.Dispose();
        }
    }
}
