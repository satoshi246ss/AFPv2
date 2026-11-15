// C# 7.3 / .NET Framework 4.8
// 依存: OpenCvSharp4 (4.5.2.20210404)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MeteorDetection
{
    /// <summary>
    /// 光害パターン除去用の「晴天時の代表的な積算平均フレーム」。
    /// 魚眼では天頂付近と地平付近、街の中心方向とそれ以外で光害の強さが異なる。
    /// この空間パターンを晴天フレームから取り込み、雲マスク作成時に
    ///   現在の背景 ≒ ゲイン × 基準フレーム + オフセット
    /// の形で（ゲイン＝センサゲイン/空の透明度、オフセット＝空全体の明るさの変化）
    /// 微調整して減算することで、光害の影響を低減する。
    ///
    /// 基準フレームは、背景画像（AccumulateWeightedの出力）と同じ輝度スケール(8bit相当)の
    /// 画像であること。解像度は異なっていてもよい（背景画像の大きさに自動で縮小される）。
    /// 恒星は雲マスク側と同じオープニングで除去されるので、基準フレームに星が写っていても構わない。
    /// </summary>
    public sealed class CloudMaskReference : IDisposable
    {
        private readonly Mat _source32;          // グレースケール(CV_32F)の元画像
        private readonly object _lock = new object();
        private float[] _prepared;               // 前処理済み(縮小→オープニング→ぼかし)のキャッシュ
        private int _pw, _ph, _pOpen, _pBlur;

        /// <summary>元の基準画像の幅 [px]</summary>
        public int Width { get { return _source32.Width; } }
        /// <summary>元の基準画像の高さ [px]</summary>
        public int Height { get { return _source32.Height; } }

        private CloudMaskReference(Mat source32)
        {
            _source32 = source32;
        }

        /// <summary>保存画像(PNG等)から基準フレームを読み込む。8bit/16bit・カラー/モノクロどれでも可
        /// （16bitは0〜65535を0〜255へ換算、カラーはグレースケール化）。</summary>
        public static CloudMaskReference FromFile(string path)
        {
            using (var m = Cv2.ImRead(path, ImreadModes.Unchanged))
            {
                if (m == null || m.Empty())
                    throw new System.IO.FileNotFoundException("基準フレームを読み込めません。", path);
                return FromMat(m);
            }
        }

        /// <summary>Matから基準フレームを作る（内部にコピーを保持するので、元のMatは破棄してよい）。
        /// CV_8U / CV_16U / CV_32F、1/3/4チャンネルに対応。32Fは0〜255スケールとして扱う。</summary>
        public static CloudMaskReference FromMat(Mat m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (m.Empty()) throw new ArgumentException("空の画像です。", nameof(m));

            Mat gray = m;
            bool disposeGray = false;
            try
            {
                if (m.Channels() == 3)
                {
                    gray = new Mat();
                    disposeGray = true;
                    Cv2.CvtColor(m, gray, ColorConversionCodes.BGR2GRAY);
                }
                else if (m.Channels() == 4)
                {
                    gray = new Mat();
                    disposeGray = true;
                    Cv2.CvtColor(m, gray, ColorConversionCodes.BGRA2GRAY);
                }

                double scale = (gray.Type() == MatType.CV_16UC1) ? 1.0 / 257.0 : 1.0;
                var f = new Mat();
                gray.ConvertTo(f, MatType.CV_32F, scale);
                return new CloudMaskReference(f);
            }
            finally
            {
                if (disposeGray) gray.Dispose();
            }
        }

        /// <summary>
        /// 背景画像と同じ前処理（縮小→恒星除去オープニング→ぼかし）を施した基準フレームを
        /// float配列で返す。同じ条件での2回目以降はキャッシュを返す。
        /// </summary>
        internal float[] GetPrepared(int w, int h, int openingSizePx, int blurSizePx)
        {
            lock (_lock)
            {
                if (_prepared != null && _pw == w && _ph == h && _pOpen == openingSizePx && _pBlur == blurSizePx)
                    return _prepared;

                var temps = new List<IDisposable>();
                try
                {
                    Mat work = _source32;

                    if (work.Width != w || work.Height != h)
                    {
                        var rs = new Mat();
                        temps.Add(rs);
                        Cv2.Resize(work, rs, new Size(w, h), 0, 0, InterpolationFlags.Area);
                        work = rs;
                    }
                    if (openingSizePx > 1)
                    {
                        var k = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(openingSizePx, openingSizePx));
                        temps.Add(k);
                        var o = new Mat();
                        temps.Add(o);
                        Cv2.MorphologyEx(work, o, MorphTypes.Open, k);
                        work = o;
                    }
                    if (blurSizePx > 1)
                    {
                        int ks = blurSizePx | 1;
                        var b = new Mat();
                        temps.Add(b);
                        Cv2.GaussianBlur(work, b, new Size(ks, ks), 0);
                        work = b;
                    }
                    if (!work.IsContinuous())
                    {
                        work = work.Clone();
                        temps.Add(work);
                    }

                    var arr = new float[w * h];
                    Marshal.Copy(work.Data, arr, 0, arr.Length);

                    _prepared = arr;
                    _pw = w; _ph = h; _pOpen = openingSizePx; _pBlur = blurSizePx;
                    return arr;
                }
                finally
                {
                    foreach (var d in temps) d.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _prepared = null;
                _source32.Dispose();
            }
        }
    }

    /// <summary>雲（月を含む）マスク生成のパラメータ。</summary>
    public sealed class CloudMaskConfig
    {
        // ---- 前処理 ----------------------------------------------------------

        /// <summary>恒星除去用オープニング(収縮→膨張)のカーネルサイズ [px]。
        /// 恒星PSFより大きく、想定する最小の雲パッチより小さく設定する。0/1で無効。</summary>
        public int OpeningSizePx = 15;

        /// <summary>オープニング後に掛けるガウシアンぼかしのカーネルサイズ [px]（量子化・ノイズ抑制）。0/1で無効。</summary>
        public int BlurSizePx = 5;

        // ---- 光害パターンの除去(晴天基準フレームの減算) ------------------------

        /// <summary>晴天時の代表的な積算平均フレーム。nullなら基準フレーム減算を行わない（従来動作）。
        /// 設定すると、現在の背景から「ゲイン×基準フレーム＋オフセット」を引いた残差で雲を判定する。</summary>
        public CloudMaskReference Reference = null;

        /// <summary>trueならゲイン(センサゲイン・空の透明度に相当)も自動推定する。
        /// falseならゲインはFixedGain固定でオフセットのみ推定する。</summary>
        public bool FitGain = true;

        /// <summary>FitGain=false のとき（または基準フレームのコントラストが乏しいとき）に使うゲイン。</summary>
        public double FixedGain = 1.0;

        /// <summary>推定ゲインの許容下限。晴天基準とかけ離れた推定は不採用側（範囲端に張り付く）に倒す。</summary>
        public double GainMin = 0.5;

        /// <summary>推定ゲインの許容上限。</summary>
        public double GainMax = 2.0;

        /// <summary>基準フレーム(前処理後)の標準偏差 [輝度] がこの値未満なら、空間パターンが乏しく
        /// ゲインを推定できないとみなし、ゲイン固定(FixedGain)でオフセットのみ推定する。</summary>
        public double ReferenceMinStd = 2.0;

        /// <summary>フィットに使う標本画素数の目安。多いほど安定するが遅くなる。</summary>
        public int FitMaxSamples = 20000;

        /// <summary>ロバストフィットの最大反復回数。</summary>
        public int FitMaxIterations = 8;

        /// <summary>フィットで、残差の中央値より下側へ何σまでを採用するか。</summary>
        public double FitClipLowSigma = 3.0;

        /// <summary>フィットで、残差の中央値より上側へ何σまでを採用するか。
        /// 雲は晴天より明るい(正の残差)ので、上側は狭めにして雲画素を締め出す。</summary>
        public double FitClipHighSigma = 2.0;

        /// <summary>フィットのσの下限 [輝度]。8bit量子化による過小推定を避ける。</summary>
        public double FitMinSigma = 0.5;

        /// <summary>フィット後の晴天残差σ [輝度] がこれを超えたら、フィット失敗（雲が空の大半を覆っている、
        /// または基準フレームが現在の空と合っていない）とみなし、基準フレーム減算を行わず従来方式に戻す。
        /// 正常時のσは1前後。ログのReferenceFitSigmaを見て調整する。</summary>
        public double FitMaxSigma = 5.0;

        /// <summary>最終的に採用された標本の割合がこれ未満なら、雲が空の大半を覆っているなどで
        /// フィットを信用できないとみなし、基準フレーム減算を行わず従来方式にフォールバックする(0〜1)。</summary>
        public double FitMinFraction = 0.25;

        /// <summary>基準フレーム減算を行った場合の MaxBaselineRise の代わりに使う値 [輝度]。
        /// 光害の勾配が除かれているので、通常は MaxBaselineRise より小さくできる。</summary>
        public double MaxBaselineRiseWithReference = 10.0;

        // ---- 晴天レベル(ベースライン)推定 -----------------------------------

        /// <summary>晴天レベル推定用のブロックサイズ [px]（背景画像の解像度基準）。
        /// 小さいほど空の明るさの勾配(地平線付近の明るさ等)に追従するが、
        /// ブロック全体が雲に覆われると晴天レベルを誤る。</summary>
        public int BlockSizePx = 64;

        /// <summary>各ブロック内で「晴天レベル」とみなす輝度のパーセンタイル(0〜1)。
        /// 雲は晴天より明るいので、低めのパーセンタイルほど晴天側を拾える。
        /// 0.30 なら、ブロックの7割が雲でなければ晴天レベルを捉えられる。</summary>
        public double ClearLevelPercentile = 0.30;

        /// <summary>全ブロックの晴天レベルから「全体の晴天レベル」を求める際のパーセンタイル(0〜1)。</summary>
        public double GlobalClearBlockPercentile = 0.25;

        /// <summary>各ブロックの晴天レベルが「全体の晴天レベル」を超えて良い上限 [輝度]。
        /// ブロック全体が雲に覆われて晴天レベルが雲の明るさに引き上げられるのを防ぐ。
        /// 地平線付近の光害による空の明るさの上昇より大きな値にすること。
        /// （基準フレーム減算が有効な場合は MaxBaselineRiseWithReference を使う）</summary>
        public double MaxBaselineRise = 30.0;

        /// <summary>晴天レベルのブロックマップに掛ける最小値フィルタの半径 [ブロック数]。
        /// 周囲のより暗い(＝晴れた)ブロックに合わせて、雲に覆われたブロックの値を下げる。0で無効。</summary>
        public int BaselineMinFilterBlocks = 1;

        /// <summary>ブロック内の有効画素(魚眼円内)がこの割合未満のブロックは統計に使わない(0〜1)。</summary>
        public double MinValidBlockFraction = 0.3;

        // ---- 二値化しきい値 ---------------------------------------------------

        /// <summary>しきい値 = 晴天レベル + max(MinOffset, OffsetSigma × 晴天ノイズσ)。
        /// 晴天ノイズσの何倍だけ晴天レベルの上に置くか。小さいほど薄い雲まで拾い、誤マスクも増える。</summary>
        public double OffsetSigma = 3.0;

        /// <summary>晴天レベルからの最小オフセット [輝度]。8bit量子化やσの過小推定に対する下限。</summary>
        public double MinOffset = 30.0;// 3.0

        // ---- 後処理 -----------------------------------------------------------

        /// <summary>断片化した雲領域を連結するクロージング(膨張→収縮)のカーネルサイズ [px]。0/1で無効。</summary>
        public int ClosingSizePx = 25;

        /// <summary>雲の輪郭の安全マージン [px]。雲の縁は2値化境界より外側に薄く広がっているため、外側に広げて除外する。</summary>
        public int MarginPx = 15;

        /// <summary>これ未満の面積 [px^2] の孤立領域は雲とみなさない。</summary>
        public int MinCloudAreaPx = 50;

        // ---- 魚眼の有効円(任意) ----------------------------------------------

        /// <summary>trueなら魚眼の像円の外側を統計から除外し、マスクでも無効(0)にする。
        /// 円の外側の暗い画素が晴天レベル推定を狂わせるのを防ぐ。</summary>
        public bool UseFovCircle = false;

        /// <summary>像円の中心X(画像幅に対する比率)</summary>
        public double FovCenterXRatio = 0.5;

        /// <summary>像円の中心Y(画像高さに対する比率)</summary>
        public double FovCenterYRatio = 0.5;

        /// <summary>像円の半径(画像幅に対する比率)。像の縁のケラレを避けるためやや小さめに設定する。</summary>
        public double FovRadiusRatio = 0.5;
    }

    /// <summary>雲マスク生成時の診断情報（ログ・パラメータ調整用）。</summary>
    public sealed class CloudMaskInfo
    {
        /// <summary>雲・月と判定された領域の割合(0〜1)。魚眼円を指定した場合は円内に対する割合。</summary>
        public double CoverageRatio;
        /// <summary>全体の晴天レベル（輝度。基準フレーム減算時は残差での値）</summary>
        public double GlobalClearLevel;
        /// <summary>推定した晴天ノイズσ（輝度）</summary>
        public double NoiseSigma;
        /// <summary>晴天レベルに加えたオフセット（輝度）</summary>
        public double Offset;
        /// <summary>晴天レベル推定に使えたブロック数</summary>
        public int UsedBlocks;
        /// <summary>全ブロック数</summary>
        public int TotalBlocks;

        /// <summary>基準フレーム減算を実際に適用したか</summary>
        public bool ReferenceUsed;
        /// <summary>推定したゲイン（基準フレームにかける係数）。未適用時は1</summary>
        public double ReferenceGain;
        /// <summary>推定したオフセット（輝度）。未適用時は0</summary>
        public double ReferenceOffset;
        /// <summary>フィット残差のσ（輝度）。ReferenceUsed=true のときだけ意味がある</summary>
        public double ReferenceFitSigma;
        /// <summary>フィットに採用された標本の割合(0〜1)。小さいほど雲が多い</summary>
        public double ReferenceFitFraction;
        /// <summary>基準フレーム減算の状態（適用/未適用の理由）</summary>
        public string ReferenceStatus = "";
    }

    /// <summary>
    /// 背景画像（AccumulateWeightedで生成した輝度平均）から雲・月を検出し、
    /// 流星検出で使う「有効領域マスク」(255=検出対象, 0=雲/月または魚眼円の外)を生成する。
    ///
    /// 考え方：
    ///  1. オープニングで恒星(点光源)を除去し、面的な明るさだけを残す
    ///  1b. （任意）晴天時の基準フレームを ゲイン×基準＋オフセット で微調整して減算し、
    ///     光害の空間パターン(天頂/地平の差、街の方向の差)を取り除いた残差画像にする
    ///  2. 画像をブロックに分け、各ブロックの低パーセンタイルから「晴天レベル」を求め、
    ///     残った緩やかな明るさの勾配に追従する滑らかなベースラインマップを作る
    ///  3. しきい値 = ベースライン + 小さなオフセット（晴天の少し上）で二値化
    ///  4. クロージング、小領域除去、安全マージンで整形
    /// </summary>
    public static class CloudMaskGenerator
    {
        // 基準フレーム減算時は残差が負にもなるため、ヒストグラムの区間を別に持つ
        private const int ResidualBins = 1024;
        private const double ResidualLo = -64.0;   // 残差ヒストグラムの下端 [輝度]
        private const double ResidualScale = 4.0;  // 1輝度あたりのビン数（0.25輝度刻み）

        // フィットで晴天の位置・σを求める分位点と、その正規分布での位置(z値)
        private const double AnchorPercentile = 0.30;
        private const double AnchorZ = -0.5244005;  // 正規分布の30%点
        private const double LowerPercentile = 0.15;
        private const double LowerZ = -1.0364334;   // 正規分布の15%点

        public static Mat GenerateValidMask(Mat background, CloudMaskConfig cfg = null,
            Action<double> cloudCoverageRatio = null, Action<CloudMaskInfo> info = null)
        {
            if (background == null) throw new ArgumentNullException(nameof(background));
            cfg = cfg ?? new CloudMaskConfig();

            var temps = new List<IDisposable>();
            Mat T(Mat m) { temps.Add(m); return m; }

            try
            {
                Mat bg8 = background;
                if (background.Type() != MatType.CV_8U)
                {
                    bg8 = T(new Mat());
                    background.ConvertTo(bg8, MatType.CV_8U);
                }

                int W = bg8.Width, H = bg8.Height;
                int total = W * H;

                // ---- 魚眼有効円 ------------------------------------------------
                Mat fov = null;
                byte[] fovBytes = null;
                if (cfg.UseFovCircle)
                {
                    fov = T(new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(0)));
                    Cv2.Circle(fov,
                        new Point((int)Math.Round(W * cfg.FovCenterXRatio), (int)Math.Round(H * cfg.FovCenterYRatio)),
                        (int)Math.Round(W * cfg.FovRadiusRatio), Scalar.All(255), -1, LineTypes.Link8);
                    fovBytes = new byte[total];
                    Marshal.Copy(fov.Data, fovBytes, 0, total);
                }

                // ---- 1. 前処理: 恒星除去 + 平滑化 -------------------------------
                Mat work = bg8;
                if (cfg.OpeningSizePx > 1)
                {
                    var k = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.OpeningSizePx, cfg.OpeningSizePx)));
                    var o = T(new Mat());
                    Cv2.MorphologyEx(work, o, MorphTypes.Open, k);
                    work = o;
                }
                if (cfg.BlurSizePx > 1)
                {
                    int ks = cfg.BlurSizePx | 1; // 奇数化
                    var b = T(new Mat());
                    Cv2.GaussianBlur(work, b, new Size(ks, ks), 0);
                    work = b;
                }
                if (!work.IsContinuous()) work = T(work.Clone());

                byte[] data = new byte[total];
                Marshal.Copy(work.Data, data, 0, total);

                // ---- 1b. 晴天基準フレームの微調整と減算 -------------------------
                RefFit fit = null;
                float[] resid = null; // 基準フレーム減算後の残差(輝度)。nullなら従来方式
                if (cfg.Reference != null)
                {
                    float[] refImg = cfg.Reference.GetPrepared(W, H, cfg.OpeningSizePx, cfg.BlurSizePx);
                    fit = FitReference(data, refImg, fovBytes, W, H, cfg);
                    if (fit.Success)
                    {
                        resid = new float[total];
                        double g = fit.Gain, off = fit.Offset;
                        for (int i = 0; i < total; i++)
                            resid[i] = (float)(data[i] - (g * refImg[i] + off));
                    }
                }
                else
                {
                    fit = new RefFit { Status = "基準フレーム未設定（減算なし）" };
                }
                bool useRef = resid != null;

                // ヒストグラムの量子化（従来: 0..255 を1刻み、基準減算時: -64..192 を0.25刻み）
                int nb = useRef ? ResidualBins : 256;
                double lo = useRef ? ResidualLo : 0.0;
                double qscale = useRef ? ResidualScale : 1.0;
                double ToValue(double bin) { return lo + bin / qscale; }

                // ---- 2. ブロックごとの晴天レベル・ノイズ推定 ---------------------
                int B = Math.Max(8, cfg.BlockSizePx);
                int bw = (W + B - 1) / B, bh = (H + B - 1) / B;
                var level = new double[bw * bh];
                var valid = new bool[bw * bh];
                var sigmaList = new List<double>();
                var levelList = new List<double>();
                var hist = new int[nb];

                for (int by = 0; by < bh; by++)
                {
                    int y0 = by * B, y1 = Math.Min(H, y0 + B);
                    for (int bx = 0; bx < bw; bx++)
                    {
                        int x0 = bx * B, x1 = Math.Min(W, x0 + B);
                        Array.Clear(hist, 0, nb);
                        int count = 0;
                        for (int y = y0; y < y1; y++)
                        {
                            int row = y * W;
                            for (int x = x0; x < x1; x++)
                            {
                                int idx = row + x;
                                if (fovBytes != null && fovBytes[idx] == 0) continue;

                                int v;
                                if (useRef)
                                {
                                    v = (int)Math.Floor((resid[idx] - lo) * qscale + 0.5);
                                    if (v < 0) v = 0; else if (v >= nb) v = nb - 1;
                                }
                                else
                                {
                                    v = data[idx];
                                }
                                hist[v]++;
                                count++;
                            }
                        }

                        int bi = by * bw + bx;
                        double blockArea = (double)(x1 - x0) * (y1 - y0);
                        if (count < 16 || count < cfg.MinValidBlockFraction * blockArea) continue;

                        double pClear = ToValue(HistPercentile(hist, count, cfg.ClearLevelPercentile));
                        double p50 = ToValue(HistPercentile(hist, count, 0.50));
                        double p16 = ToValue(HistPercentile(hist, count, 0.1587)); // 正規分布の-1σ

                        level[bi] = pClear;
                        valid[bi] = true;
                        levelList.Add(pClear);
                        sigmaList.Add(Math.Max(0.0, p50 - p16));
                    }
                }

                // 統計が取れない(魚眼円が極端に小さい等)場合は、全域有効で返す
                if (levelList.Count == 0)
                {
                    var allValid = new Mat(bg8.Size(), MatType.CV_8U, Scalar.All(255));
                    if (fov != null) Cv2.BitwiseAnd(allValid, fov, allValid);
                    cloudCoverageRatio?.Invoke(0.0);
                    info?.Invoke(MakeInfo(fit, useRef, 0.0, 0.0, 0.0, 0.0, 0, bw * bh));
                    return allValid;
                }

                double globalClear = ListPercentile(levelList, cfg.GlobalClearBlockPercentile);
                // 晴天ノイズσ: 晴れたブロックほどσが小さいため、下位25%点(外れ値に頑健)を採用
                double sigma = ListPercentile(sigmaList, 0.25);
                double offset = Math.Max(cfg.MinOffset, cfg.OffsetSigma * sigma);

                // ---- 3. ベースラインマップ (ブロック→全画素へ補間) ----------------
                var thr = new float[bw * bh];
                double rise = useRef ? cfg.MaxBaselineRiseWithReference : cfg.MaxBaselineRise;
                double cap = globalClear + rise;
                for (int i = 0; i < thr.Length; i++)
                {
                    double bl = valid[i] ? Math.Min(level[i], cap) : globalClear;
                    thr[i] = (float)(bl + offset); // しきい値 = 晴天レベル + オフセット
                }

                Mat small = T(new Mat(bh, bw, MatType.CV_32F));
                Marshal.Copy(thr, 0, small.Data, thr.Length);

                if (cfg.BaselineMinFilterBlocks > 0)
                {
                    int ke = cfg.BaselineMinFilterBlocks * 2 + 1;
                    var kern = T(Cv2.GetStructuringElement(MorphShapes.Rect, new Size(ke, ke)));
                    Cv2.Erode(small, small, kern); // 周囲の最も暗い(晴れた)ブロックに合わせる
                }
                if (bw >= 3 && bh >= 3)
                    Cv2.GaussianBlur(small, small, new Size(3, 3), 0);

                Mat thrFull = T(new Mat());
                Cv2.Resize(small, thrFull, new Size(W, H), 0, 0, InterpolationFlags.Linear);

                // ---- 4. 二値化 (判定画像 > しきい値 を雲候補とする) ---------------
                Mat img32;
                if (useRef)
                {
                    img32 = T(new Mat(H, W, MatType.CV_32F));
                    Marshal.Copy(resid, 0, img32.Data, total);
                }
                else
                {
                    img32 = T(new Mat());
                    work.ConvertTo(img32, MatType.CV_32F);
                }

                Mat cloudBin = T(new Mat());
                Cv2.Compare(img32, thrFull, cloudBin, CmpType.GT);

                if (fov != null) Cv2.BitwiseAnd(cloudBin, fov, cloudBin);

                // 断片化した雲を連結
                if (cfg.ClosingSizePx > 1)
                {
                    var ck = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.ClosingSizePx, cfg.ClosingSizePx)));
                    Cv2.MorphologyEx(cloudBin, cloudBin, MorphTypes.Close, ck);
                }

                // ---- 5. 小領域除去（雲内部の晴れ間は保持される） -------------------
                Mat labels = T(new Mat());
                Mat stats = T(new Mat());
                Mat cents = T(new Mat());
                int n = Cv2.ConnectedComponentsWithStats(cloudBin, labels, stats, cents);

                var keep = new bool[n];
                for (int i = 1; i < n; i++)
                    keep[i] = stats.At<int>(i, (int)ConnectedComponentsTypes.Area) >= cfg.MinCloudAreaPx;

                var lab = new int[total];
                Marshal.Copy(labels.Data, lab, 0, total);
                var cloudBytes = new byte[total];
                for (int i = 0; i < total; i++)
                    if (keep[lab[i]]) cloudBytes[i] = 255;

                Mat cloudMask = T(new Mat(bg8.Size(), MatType.CV_8U));
                Marshal.Copy(cloudBytes, 0, cloudMask.Data, total);

                // ---- 雲占有率 (安全マージン適用前) --------------------------------
                double coverage;
                if (fov != null)
                {
                    int fovPx = Cv2.CountNonZero(fov);
                    coverage = fovPx > 0 ? (double)Cv2.CountNonZero(cloudMask) / fovPx : 0.0;
                }
                else
                {
                    coverage = (double)Cv2.CountNonZero(cloudMask) / total;
                }
                cloudCoverageRatio?.Invoke(coverage);

                // ---- 6. 安全マージン: 雲領域を外側へ広げる -------------------------
                if (cfg.MarginPx > 1)
                {
                    var mk = T(Cv2.GetStructuringElement(MorphShapes.Ellipse,
                        new Size(cfg.MarginPx, cfg.MarginPx)));
                    Cv2.Dilate(cloudMask, cloudMask, mk);
                }

                var validMask = new Mat();
                Cv2.BitwiseNot(cloudMask, validMask);
                if (fov != null) Cv2.BitwiseAnd(validMask, fov, validMask);

                info?.Invoke(MakeInfo(fit, useRef, coverage, globalClear, sigma, offset,
                    levelList.Count, bw * bh));

                return validMask;
            }
            finally
            {
                foreach (var d in temps) d.Dispose();
            }
        }

        // ====================================================================
        //  基準フレームのゲイン・オフセット推定
        // ====================================================================

        private sealed class RefFit
        {
            public bool Success;
            public double Gain = 1.0;
            public double Offset = 0.0;
            public double Sigma;
            public double UsedFraction;
            public string Status = "";
        }

        private static CloudMaskInfo MakeInfo(RefFit fit, bool useRef, double coverage,
            double globalClear, double sigma, double offset, int usedBlocks, int totalBlocks)
        {
            return new CloudMaskInfo
            {
                CoverageRatio = coverage,
                GlobalClearLevel = globalClear,
                NoiseSigma = sigma,
                Offset = offset,
                UsedBlocks = usedBlocks,
                TotalBlocks = totalBlocks,
                ReferenceUsed = useRef,
                ReferenceGain = useRef ? fit.Gain : 1.0,
                ReferenceOffset = useRef ? fit.Offset : 0.0,
                ReferenceFitSigma = fit != null ? fit.Sigma : 0.0,
                ReferenceFitFraction = fit != null ? fit.UsedFraction : 0.0,
                ReferenceStatus = fit != null ? fit.Status : ""
            };
        }

        /// <summary>
        /// 現在の背景(cur)を  cur ≒ gain × ref + offset  で近似し、gain/offset を推定する。
        /// 雲は晴天より必ず明るい(残差が正に大きい)ので、残差の「下側の分位点」を晴天の位置として基準にし、
        /// 上側を狭く・下側を広くとった窓に入る画素だけで最小二乗フィットし直すことを繰り返す
        /// （片側トリミング付き反復最小二乗）。
        /// 晴天の位置とσは 30%点・15%点（正規分布なら μ-0.52σ、μ-1.04σ）から求めるため、
        /// 空の約60%まで雲に覆われていても推定できる（中央値基準だと約40%が限界だった）。
        /// 晴天が約30%を下回ると原理的に推定できず、残差σが異常に大きくなるので、
        /// FitMaxSigma を超えたら不採用（従来方式へフォールバック）とする。
        /// </summary>
        private static RefFit FitReference(byte[] cur, float[] refImg, byte[] fovBytes, int W, int H,
            CloudMaskConfig cfg)
        {
            var fit = new RefFit();
            int total = W * H;

            // --- 規則格子で標本をとる（魚眼円の外は除く） ---
            int target = Math.Max(500, cfg.FitMaxSamples);
            int step = Math.Max(1, (int)Math.Sqrt((double)total / target));
            int cap = ((H + step - 1) / step) * ((W + step - 1) / step);
            var xs = new double[cap];
            var ys = new double[cap];
            int n = 0;
            for (int y = step / 2; y < H; y += step)
            {
                int row = y * W;
                for (int x = step / 2; x < W; x += step)
                {
                    int idx = row + x;
                    if (fovBytes != null && fovBytes[idx] == 0) continue;
                    xs[n] = refImg[idx];
                    ys[n] = cur[idx];
                    n++;
                }
            }
            if (n < 200)
            {
                fit.Status = "基準フレーム未適用: フィット用の標本が不足";
                return fit;
            }

            // --- 基準フレームのコントラスト（空間パターンの強さ） ---
            double mx = 0, mxx = 0;
            for (int i = 0; i < n; i++) { mx += xs[i]; mxx += xs[i] * xs[i]; }
            mx /= n; mxx /= n;
            double stdX = Math.Sqrt(Math.Max(0.0, mxx - mx * mx));

            bool fitGain = cfg.FitGain && stdX >= cfg.ReferenceMinStd;
            double gmin = cfg.GainMin, gmax = cfg.GainMax;
            double g = fitGain ? Clamp(1.0, gmin, gmax) : cfg.FixedGain;
            double o = 0.0; // 初期値は不問（中央値基準で窓を取るため）

            var r = new double[n];
            var sorted = new double[n];
            int ns = 0;
            double sigma = 0;
            bool converged = false;
            int maxIter = Math.Max(1, cfg.FitMaxIterations);

            for (int it = 0; it < maxIter; it++)
            {
                for (int i = 0; i < n; i++) r[i] = ys[i] - (g * xs[i] + o);
                Array.Copy(r, sorted, n);
                Array.Sort(sorted);

                double pc = sorted[(int)(AnchorPercentile * n)];
                double pl = sorted[(int)(LowerPercentile * n)];
                sigma = Math.Max(cfg.FitMinSigma, (pc - pl) / (AnchorZ - LowerZ));
                double mu = pc - AnchorZ * sigma; // 晴天の残差の中心（AnchorZは負）
                double lowB = mu - cfg.FitClipLowSigma * sigma;
                double highB = mu + cfg.FitClipHighSigma * sigma;

                double sx = 0, sy = 0, sxx = 0, sxy = 0;
                ns = 0;
                for (int i = 0; i < n; i++)
                {
                    if (r[i] < lowB || r[i] > highB) continue;
                    ns++;
                    sx += xs[i]; sy += ys[i];
                    sxx += xs[i] * xs[i]; sxy += xs[i] * ys[i];
                }
                if (ns < 50)
                {
                    fit.Status = "基準フレーム未適用: フィットに使える画素が不足";
                    return fit;
                }

                double ng = g;
                if (fitGain)
                {
                    double denom = ns * sxx - sx * sx;
                    if (denom > 0.25 * ns * (double)ns) // 採用画素内の基準フレームの分散が十分あるとき
                        ng = Clamp((ns * sxy - sx * sy) / denom, gmin, gmax);
                }
                double no = (sy - ng * sx) / ns;

                bool done = Math.Abs(ng - g) < 1e-4 && Math.Abs(no - o) < 0.01;
                g = ng; o = no;
                if (done) { converged = true; break; }
            }

            fit.Gain = g;
            fit.Offset = o;
            fit.Sigma = sigma;
            fit.UsedFraction = (double)ns / n;

            if (sigma > cfg.FitMaxSigma)
            {
                fit.Status = "基準フレーム未適用: 残差σが大きすぎる（σ=" + sigma.ToString("F1")
                    + "）。雲が多い、または基準フレームが現在の空と合っていない";
                return fit;
            }
            if (fit.UsedFraction < cfg.FitMinFraction)
            {
                fit.Status = "基準フレーム未適用: 雲が多くフィットを信用できない（採用率 "
                    + fit.UsedFraction.ToString("P0") + "）";
                return fit;
            }

            fit.Success = true;
            string note = fitGain ? "" : "（ゲイン固定）";
            if (fitGain && (g <= gmin + 1e-6 || g >= gmax - 1e-6)) note += " ※ゲインが許容範囲の端";
            fit.Status = (converged ? "基準フレーム適用" : "基準フレーム適用(未収束)") + note;
            return fit;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        // ====================================================================
        //  統計ユーティリティ
        // ====================================================================

        /// <summary>ヒストグラムからパーセンタイルをビン単位で求める（各ビンを幅1の区間として補間）。
        /// 戻り値はビン番号の空間（ビン v は v-0.5 〜 v+0.5）。</summary>
        private static double HistPercentile(int[] hist, int count, double p)
        {
            double target = p * count;
            double cum = 0;
            for (int v = 0; v < hist.Length; v++)
            {
                double next = cum + hist[v];
                if (hist[v] > 0 && next >= target)
                {
                    double frac = (target - cum) / hist[v];
                    return v - 0.5 + frac;
                }
                cum = next;
            }
            return hist.Length - 1.0;
        }

        private static double ListPercentile(List<double> values, double p)
        {
            var a = values.ToArray();
            Array.Sort(a);
            if (a.Length == 1) return a[0];
            double pos = Math.Max(0.0, Math.Min(1.0, p)) * (a.Length - 1);
            int i0 = (int)Math.Floor(pos);
            int i1 = Math.Min(a.Length - 1, i0 + 1);
            double f = pos - i0;
            return a[i0] * (1 - f) + a[i1] * f;
        }
    }
}
