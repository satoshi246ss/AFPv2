// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>1フレーム分の検出結果（フル解像度座標系）。</summary>
    public struct Detection
    {
        public Point2f Center;   // 重心 [px] (フル解像度)
        public Rect BBox;        // 外接矩形 [px] (フル解像度)
        public double AreaPx;    // 面積 [px^2] (フル解像度換算)
        public double PeakValue; // ROI内の背景差分ピーク値（明るさの目安）
    }

    /// <summary>
    /// 指数移動平均による軽量背景モデル。1ピクセルあたり1回の積和のみで、
    /// 中央値法やフレームバッファ法よりはるかに低負荷。
    /// </summary>
    internal sealed class RunningAverageBackground
    {
        private Mat _bgF32;
        private readonly double _alpha;

        public RunningAverageBackground(double alpha = 0.03)
        {
            _alpha = alpha;
        }

        /// <returns>更新後の背景 (CV_32F)</returns>
        public Mat Update(Mat frame8u)
        {
            if (_bgF32 == null || _bgF32.Size() != frame8u.Size())
            {
                _bgF32?.Dispose();
                _bgF32 = new Mat();
                frame8u.ConvertTo(_bgF32, MatType.CV_32F);
            }
            else
            {
                Cv2.AccumulateWeighted(frame8u, _bgF32, _alpha);
            }
            return _bgF32;
        }
    }

    /// <summary>
    /// 低負荷な流星・火球候補検出器。
    /// 処理: ①縮小(ビニング) → ②背景差分 → ③動的しきい値2値化 → ④膨張 → ⑤連結成分抽出 → ⑥面積フィルタ。
    /// 2608x2608を毎フレーム丸ごと処理せず、検出段は縮小画像で行うことでCPU負荷を大きく下げる
    /// （BinFactor=2で画素数1/4、BinFactor=4で1/16）。確定後の画像保存はフル解像度を使う。
    /// </summary>
    public sealed class MeteorFrameDetector
    {
        /// <summary>検出処理時の縮小倍率（2以上推奨。1=縮小なし）</summary>
        public int BinFactor { get; set; } = 2;

        /// <summary>動的しきい値 = 平均 + ThresholdK * 標準偏差</summary>
        public double ThresholdK { get; set; } = 6.0;

        /// <summary>動的しきい値の下限（輝度差、8bit階調換算）</summary>
        public double MinThreshold { get; set; } = 12.0;

        /// <summary>最小面積（縮小後座標系, px^2）。ノイズ除去用</summary>
        public int MinAreaPx { get; set; } = 2;

        /// <summary>最大面積（縮小後座標系, px^2）。雲・鳥・大型光源の除外用</summary>
        public int MaxAreaPx { get; set; } = 600;

        /// <summary>膨張カーネルサイズ（縮小後座標系, px）。0で膨張なし</summary>
        public int DilateSize { get; set; } = 3;

        /// <summary>背景更新の指数移動平均係数</summary>
        public double BackgroundAlpha { get; set; } = 0.03;

        private RunningAverageBackground _bg;
        private Mat _small, _bgSmall32, _bgSmall8, _diff, _mask, _kernel;
        private int _kernelSize = -1;

        public MeteorFrameDetector()
        {
            _bg = new RunningAverageBackground(BackgroundAlpha);
        }

        /// <summary>1フレームを処理して検出リストを返す（フル解像度座標）。</summary>
        public List<Detection> Process(Mat frameGray8u)
        {
            int bf = Math.Max(1, BinFactor);
            var size = new Size(Math.Max(1, frameGray8u.Width / bf), Math.Max(1, frameGray8u.Height / bf));

            if (_small == null || _small.Size() != size)
            {
                _small?.Dispose();
                _small = new Mat(size, MatType.CV_8U);
            }

            if (bf > 1)
                Cv2.Resize(frameGray8u, _small, size, 0, 0, InterpolationFlags.Area);
            else
                frameGray8u.CopyTo(_small);

            var bg32 = _bg.Update(_small);

            if (_bgSmall8 == null || _bgSmall8.Size() != size)
            {
                _bgSmall8?.Dispose();
                _bgSmall8 = new Mat();
            }
            bg32.ConvertTo(_bgSmall8, MatType.CV_8U);

            if (_diff == null || _diff.Size() != size)
            {
                _diff?.Dispose();
                _diff = new Mat();
            }
            Cv2.Absdiff(_small, _bgSmall8, _diff);

            Cv2.MeanStdDev(_diff, out Scalar mean, out Scalar std);
            double thresh = Math.Max(MinThreshold, mean.Val0 + ThresholdK * std.Val0);

            if (_mask == null || _mask.Size() != size)
            {
                _mask?.Dispose();
                _mask = new Mat();
            }
            Cv2.Threshold(_diff, _mask, thresh, 255, ThresholdTypes.Binary);

            if (DilateSize > 0)
            {
                if (_kernel == null || _kernelSize != DilateSize)
                {
                    _kernel?.Dispose();
                    _kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(DilateSize, DilateSize));
                    _kernelSize = DilateSize;
                }
                Cv2.Dilate(_mask, _mask, _kernel);
            }

            var results = new List<Detection>();

            using (var labels = new Mat())
            using (var stats = new Mat())
            using (var centroids = new Mat())
            {
                int n = Cv2.ConnectedComponentsWithStats(_mask, labels, stats, centroids);

                for (int i = 1; i < n; i++) // ラベル0=背景
                {
                    int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                    if (area < MinAreaPx || area > MaxAreaPx) continue;

                    int left = stats.At<int>(i, (int)ConnectedComponentsTypes.Left);
                    int top = stats.At<int>(i, (int)ConnectedComponentsTypes.Top);
                    int w = stats.At<int>(i, (int)ConnectedComponentsTypes.Width);
                    int h = stats.At<int>(i, (int)ConnectedComponentsTypes.Height);

                    double cx = centroids.At<double>(i, 0);
                    double cy = centroids.At<double>(i, 1);

                    double peak;
                    using (var roi = new Mat(_diff, new Rect(left, top, w, h)))
                    {
                        Cv2.MinMaxLoc(roi, out double _, out double maxV);
                        peak = maxV;
                    }

                    results.Add(new Detection
                    {
                        Center = new Point2f((float)(cx * bf), (float)(cy * bf)),
                        BBox = new Rect(left * bf, top * bf, w * bf, h * bf),
                        AreaPx = (double)area * bf * bf,
                        PeakValue = peak
                    });
                }
            }

            return results;
        }
    }
}
