// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>比較明画像作成のオプション。</summary>
    public sealed class LightenOptions
    {
        /// <summary>開始フレーム番号（0始まり）。</summary>
        public int StartFrame = 0;

        /// <summary>終了フレーム番号（この番号を含む）。-1で最後まで。</summary>
        public int EndFrame = -1;

        /// <summary>フレームの間引き間隔（1=全フレーム）。</summary>
        public int FrameStep = 1;

        /// <summary>trueならグレースケールに変換してから合成する（カラー動画でも出力は1チャンネル）。</summary>
        public bool Grayscale = false;

        /// <summary>trueなら平均画像と「比較明−平均」画像も作る。
        /// 比較明画像では恒星が全て線状に残って流星が埋もれやすいが、平均を引くと
        /// 動かない恒星・光害が消え、流星の軌跡だけが際立つ。</summary>
        public bool MakeDifferenceImage = true;

        /// <summary>進捗コールバック（処理済みフレーム数, 総フレーム数。総数不明なら-1）。不要ならnull。</summary>
        public Action<int, int> Progress = null;
    }

    /// <summary>比較明画像作成の結果。使い終わったらDisposeすること。</summary>
    public sealed class LightenResult : IDisposable
    {
        /// <summary>比較明画像（各画素の全フレームでの最大値）。</summary>
        public Mat Lighten;

        /// <summary>平均画像（MakeDifferenceImage=true のときのみ）。</summary>
        public Mat Mean;

        /// <summary>比較明−平均 画像（MakeDifferenceImage=true のときのみ）。流星確認用。</summary>
        public Mat Difference;

        /// <summary>合成に使ったフレーム数。</summary>
        public int FramesUsed;

        /// <summary>動画のフレームレート [fps]（取得できなければ0）。</summary>
        public double Fps;

        /// <summary>合成した最初のフレーム番号。</summary>
        public int FirstFrameIndex;

        /// <summary>合成した最後のフレーム番号。</summary>
        public int LastFrameIndex;

        /// <summary>比較明画像をファイルへ保存する（拡張子で形式決定。PNG推奨）。</summary>
        public void SaveLighten(string path) { Cv2.ImWrite(path, Lighten); }

        /// <summary>差分画像をファイルへ保存する。</summary>
        public void SaveDifference(string path)
        {
            if (Difference == null) throw new InvalidOperationException("差分画像は作成されていません。");
            Cv2.ImWrite(path, Difference);
        }

        public void Dispose()
        {
            Lighten?.Dispose();
            Mean?.Dispose();
            Difference?.Dispose();
        }
    }

    /// <summary>
    /// 観測動画から比較明合成画像を作る。流星の軌跡確認用。
    /// 処理は フレーム読込 → Cv2.Max(積算) のみで軽量（1フレームあたり1回の画素ごと最大値）。
    /// メモリはフレーム数によらず一定（最大値画像 + 平均用float画像）で、
    /// 数秒〜数分程度の動画なら問題なく処理できる。
    /// </summary>
    public static class VideoLightenComposite
    {
        /// <summary>
        /// 動画ファイルから比較明画像を作成する。
        /// </summary>
        /// <param name="videoPath">動画ファイルのパス</param>
        /// <param name="options">オプション。省略時は全フレーム・差分画像も作成</param>
        public static LightenResult Create(string videoPath, LightenOptions options = null)
        {
            if (string.IsNullOrEmpty(videoPath)) throw new ArgumentNullException(nameof(videoPath));
            options = options ?? new LightenOptions();

            using (var capture = new VideoCapture(videoPath))
            {
                if (!capture.IsOpened())
                    throw new System.IO.IOException("動画を開けませんでした: " + videoPath);

                int totalFrames = capture.FrameCount; // コーデックによっては不正確/0
                int step = Math.Max(1, options.FrameStep);
                int start = Math.Max(0, options.StartFrame);

                Mat lighten = null;
                Mat sum32 = null; // 平均用の積算(CV_32F)
                int used = 0;
                int first = -1, last = -1;

                try
                {
                    int index = -1;
                    using (var frame = new Mat())
                    using (var gray = new Mat())
                    {
                        while (true)
                        {
                            if (!capture.Read(frame) || frame.Empty()) break;
                            index++;

                            if (index < start) continue;
                            if (options.EndFrame >= 0 && index > options.EndFrame) break;
                            if ((index - start) % step != 0) continue;

                            Mat src = frame;
                            if (options.Grayscale && frame.Channels() > 1)
                            {
                                Cv2.CvtColor(frame, gray, frame.Channels() == 4
                                    ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
                                src = gray;
                            }

                            if (lighten == null)
                            {
                                lighten = src.Clone();
                                if (options.MakeDifferenceImage)
                                {
                                    sum32 = new Mat();
                                    src.ConvertTo(sum32, MatType.CV_32FC(src.Channels()));
                                }
                                first = index;
                            }
                            else
                            {
                                Cv2.Max(lighten, src, lighten);
                                if (sum32 != null) Cv2.Accumulate(src, sum32);
                            }

                            used++;
                            last = index;
                            options.Progress?.Invoke(used, totalFrames > 0 ? totalFrames : -1);
                        }
                    }

                    if (lighten == null)
                        throw new InvalidOperationException("指定範囲に読み込めるフレームがありませんでした。");

                    var result = new LightenResult
                    {
                        Lighten = lighten,
                        FramesUsed = used,
                        Fps = capture.Fps,
                        FirstFrameIndex = first,
                        LastFrameIndex = last
                    };
                    lighten = null; // 所有権をresultへ移した

                    if (sum32 != null)
                    {
                        result.Mean = new Mat();
                        sum32.ConvertTo(result.Mean, result.Lighten.Type(), 1.0 / used);

                        result.Difference = new Mat();
                        Cv2.Subtract(result.Lighten, result.Mean, result.Difference);
                    }

                    return result;
                }
                finally
                {
                    lighten?.Dispose();
                    sum32?.Dispose();
                }
            }
        }

        /// <summary>
        /// 動画から比較明画像を作成し、そのままPNGに保存する簡易版。
        /// 差分画像は「（出力パス拡張子の前）_diff.png」として保存する。
        /// </summary>
        public static void CreateAndSave(string videoPath, string outputLightenPath, LightenOptions options = null)
        {
            using (var r = Create(videoPath, options))
            {
                r.SaveLighten(outputLightenPath);
                if (r.Difference != null)
                {
                    string dir = System.IO.Path.GetDirectoryName(outputLightenPath) ?? "";
                    string name = System.IO.Path.GetFileNameWithoutExtension(outputLightenPath);
                    string ext = System.IO.Path.GetExtension(outputLightenPath);
                    r.SaveDifference(System.IO.Path.Combine(dir, name + "_diff" + ext));
                }
            }
        }
    }
}
