// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
//
// CPUのみ(内蔵GPU 496MB, RAM 8GB)という条件を踏まえた構成例：
//  ・撮像(フレーム取得)と検出処理を別スレッドに分離し、フレーム取得が
//    検出処理の遅延で止まらないようにする（コマ落ちはリングバッファ側で吸収）。
//  ・BlockingCollectionのキュー長を短くし、処理が追い付かない場合は
//    古いフレームを捨てて最新優先にする（流星検知はリアルタイム性が重要なため）。
using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenCvSharp;
using AllSkyCamera;

namespace MeteorDetection
{
    public static class UsageExample
    {
        public static void Run(string videoSource, string trackerCameraHost, int trackerCameraPort,
            string outputBaseDir, FisheyeCameraModel cameraModel)
        {
            var detector = new MeteorFrameDetector
            {
                BinFactor = 2,      // 2608x2608 -> 1304x1304 で検出処理（CPU負荷を約1/4に）
                ThresholdK = 6.0,
                MinAreaPx = 2,
                MaxAreaPx = 600,
                DilateSize = 3
            };

            var trackerCfg = new TrackManagerConfig
            {
                GateRadiusPx = 60,
                MinHitsToConfirm = 3,
                MaxMissesTentative = 2,
                MaxMissesConfirmed = 4,
                MinSpeedPxPerSec = 20,
                MaxSpeedPxPerSec = 4000
            };
            var tracker = new TrackManager(trackerCfg);

            var recorder = new DetectionRecorder(outputBaseDir);
            var udp = new MeteorUdpSender(trackerCameraHost, trackerCameraPort);

            using (var pipeline = new MeteorDetectionPipeline(detector, tracker, recorder, udp, cameraModel))
            using (var capture = new VideoCapture(videoSource))
            {
                if (!capture.IsOpened())
                    throw new InvalidOperationException("映像ソースを開けませんでした: " + videoSource);

                // 取得フレームを処理スレッドへ渡すキュー（最新優先、溜め込みすぎない）
                var queue = new BlockingCollection<(Mat frame, DateTime t)>(boundedCapacity: 3);
                using (var cts = new CancellationTokenSource())
                {
                    var grabThread = new Thread(() => GrabLoop(capture, queue, cts.Token)) { IsBackground = true };
                    var processThread = new Thread(() => ProcessLoop(pipeline, queue, cts.Token)) { IsBackground = true };

                    grabThread.Start();
                    processThread.Start();

                    Console.WriteLine("流星検知を開始しました。Enterキーで終了します。");
                    Console.ReadLine();

                    cts.Cancel();
                    queue.CompleteAdding();
                    grabThread.Join();
                    processThread.Join();
                }
            }
        }

        private static void GrabLoop(VideoCapture capture, BlockingCollection<(Mat frame, DateTime t)> queue,
            CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                {
                    frame.Dispose();
                    break;
                }

                var gray = frame;
                if (frame.Channels() > 1)
                {
                    gray = new Mat();
                    Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                    frame.Dispose();
                }

                var t = DateTime.UtcNow;

                // キューが満杯なら最古のフレームを捨てて最新を優先する（リアルタイム性重視）
                if (queue.Count >= 3 && queue.TryTake(out var old))
                    old.frame.Dispose();

                if (!queue.TryAdd((gray, t)))
                    gray.Dispose();
            }
        }

        private static void ProcessLoop(MeteorDetectionPipeline pipeline,
            BlockingCollection<(Mat frame, DateTime t)> queue, CancellationToken token)
        {
            foreach (var item in queue.GetConsumingEnumerable(token))
            {
                try
                {
                    pipeline.ProcessFrame(item.frame, item.t);
                }
                finally
                {
                    item.frame.Dispose();
                }
            }
        }
    }
}
