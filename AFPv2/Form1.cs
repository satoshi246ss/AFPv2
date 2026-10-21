//using PylonC.NETSupportLibrary;
//using MtLibrary;
using MeteorDetection;
using MtLibrary2;
using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
//using OpenCvSharp.Blob;
//using VideoInputSharp;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace AFPv2                                                                                                                                                                                                                                                                                                 
{
    public partial class Form1 : Form
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        Star star = new Star();

        public AllSkyCamera.FisheyeCameraModel fish2 = new AllSkyCamera.FisheyeCameraModel(2608, 2608, 2.7 / 0.00345);

        // オーバーレイ表示用ビットマップとロック
        private Bitmap overlayBitmap = null;
        private readonly object overlayLock = new object();
        // 動画検出用の共有 detector（UI で制御）
        private MeteorDetection.MeteorFrameDetector videoDetector;
        private volatile bool showMaskOverlay;
        private volatile bool useStarMask;
        private readonly Dictionary<string, NumericUpDown> cloudMaskControls = new Dictionary<string, NumericUpDown>();
        private Panel cloudMaskPanel;
        // デザイナーで追加したビデオ検出ボタンのフィールド（Designer にも同様の初期化がある）
        private System.Windows.Forms.Button buttonDetectFromVideo;
        // グリッド表示用チェックボックス（ランタイム生成）
        //private System.Windows.Forms.CheckBox checkBoxGrid = null;
        // loupe用の診断ログを間引くためのタイムスタンプ
        private DateTime lastLoupeLog = DateTime.MinValue;
        // 星位置計算の間隔制御
        private DateTime lastStarCalc = DateTime.MinValue;
        private double starCalcIntervalSec = 3.0; // 数秒ごとに再計算（変更可）
        private double loupeZoom = 4.0; // loupeのズーム倍率（変更可）
        // List to store clicked pixel positions
        private System.Collections.Generic.List<System.Drawing.Point> clickedPixels = new System.Collections.Generic.List<System.Drawing.Point>();
        // Last loupe point updated via Shift+MouseMove (-1,-1 means none)
        private System.Drawing.Point loupePoint = new System.Drawing.Point(-1, -1);
    // ルーペの表示中心を保持（PictureBox のクライアント座標）
    private System.Drawing.Point loupeCenter = new System.Drawing.Point(-1, -1);
    private bool loupeCenterInitialized = false;
    // ルーペ更新用タイマー（4fps）
    private System.Windows.Forms.Timer loupeTimer = null;
        // Brightness / Contrast UI controls (moved to Designer)


        public Form1()
        {
            InitializeComponent();
            timeBeginPeriod(time_period);

            //コマンドライン引数を配列で取得する
            cmds = System.Environment.GetCommandLineArgs();
            //コマンドライン引数をcheck
            if (cmds.Length != 3)
            {
                //アプリケーションを終了する
                Application.Exit();
            }
        
            if (cmds[1].StartsWith("/vi") || cmds[1].StartsWith("/an"))  // analog camera VideoInputを使用
            {
                cam_maker = Camera_Maker.analog;
                // cam_color = Camera_Color.mono;
            }
            if (cmds[1].StartsWith("/PG") || cmds[1].StartsWith("/Pg") || cmds[1].StartsWith("/pg") || cmds[1].StartsWith("/pgr")) // PointGreyReserch
            {
                cam_maker = Camera_Maker.PointGreyCamera;
                //PgrPrintBuildInfo();
                logger.Info("PointGrayCamera start.");
            }
            if (cmds[1].StartsWith("/BA") || cmds[1].StartsWith("/ba") || cmds[1].StartsWith("/Ba")) // Basler
            {
                cam_maker = Camera_Maker.Basler;
                // cam_color = Camera_Color.mono;
                //updateDeviceListTimer.Enabled = true;
            }
            if (cmds[1].StartsWith("/AV") || cmds[1].StartsWith("/av")) // AVT
            {
                cam_maker = Camera_Maker.AVT;
                // cam_color = Camera_Color.mono;
            }
            if (cmds[1].StartsWith("/ID") || cmds[1].StartsWith("/id")) // IDS
            {
                cam_maker = Camera_Maker.IDS;
                // cam_color = Camera_Color.mono;
                logger.Info("IDS camera start.");
            }
            if (cmds[1].StartsWith("/IS") || cmds[1].StartsWith("/Im")) // Imaging Souce
            {
                cam_maker = Camera_Maker.ImagingSouce;
                // cam_color = Camera_Color.mono;
            }

            // setting load
            appSettings = SettingsLoad(int.Parse(cmds[2]));

            // initialize loupe controls defaults (handled elsewhere)

            IplImageInit();

            // 動画検出用のデフォルト detector を作成し、UI に反映・イベント登録
            try
            {
                videoDetector = new MeteorDetection.MeteorFrameDetector();
                videoDetector.CloudMaskConfig = new MeteorDetection.CloudMaskConfig();
                videoDetector.StarMaskConfig = new MeteorDetection.StarMaskConfig();
                if (this.checkBoxShowMaskOverlay != null)
                {
                    this.checkBoxShowMaskOverlay.CheckedChanged += (s, e) => showMaskOverlay = this.checkBoxShowMaskOverlay.Checked;
                    showMaskOverlay = this.checkBoxShowMaskOverlay.Checked;
                }
                if (this.checkBoxUseStarMask != null)
                {
                    this.checkBoxUseStarMask.CheckedChanged += (s, e) =>
                    {
                        useStarMask = this.checkBoxUseStarMask.Checked;
                        if (videoDetector != null)
                            videoDetector.StarMaskConfig = useStarMask ? new MeteorDetection.StarMaskConfig() : null;
                    };
                    useStarMask = this.checkBoxUseStarMask.Checked;
                    videoDetector.StarMaskConfig = useStarMask ? new MeteorDetection.StarMaskConfig() : null;
                }
                if (this.numericUpDownDetBin != null) this.numericUpDownDetBin.Value = videoDetector.BinFactor;
                if (this.numericUpDownDetThreshK != null) this.numericUpDownDetThreshK.Value = (decimal)videoDetector.ThresholdK;
                if (this.numericUpDownDetMinTh != null) this.numericUpDownDetMinTh.Value = (decimal)videoDetector.MinThreshold;
                if (this.numericUpDownDetMinArea != null) this.numericUpDownDetMinArea.Value = videoDetector.MinAreaPx;
                if (this.numericUpDownDetDilate != null) this.numericUpDownDetDilate.Value = videoDetector.DilateSize;
                InitializeCloudMaskControls();
            }
            catch { }

            // Brightness/Contrast controls are managed by the Designer now.
            // Wire up event handlers and initial sync between TrackBar and NumericUpDown
            try
            {
                if (this.trackBarContrast != null && this.numericUpDownContrast != null)
                {
                    this.numericUpDownContrast.Minimum = new decimal(this.trackBarContrast.Minimum);
                    this.numericUpDownContrast.Maximum = new decimal(this.trackBarContrast.Maximum);
                    if (this.trackBarContrast.Value < this.trackBarContrast.Minimum) this.trackBarContrast.Value = this.trackBarContrast.Minimum;
                    if (this.trackBarContrast.Value > this.trackBarContrast.Maximum) this.trackBarContrast.Value = this.trackBarContrast.Maximum;
                    try { this.numericUpDownContrast.Value = new decimal(this.trackBarContrast.Value); } catch { }
                    this.trackBarContrast.Scroll += (s, ev) => { try { this.numericUpDownContrast.Value = new decimal(this.trackBarContrast.Value); } catch { } };
                    this.numericUpDownContrast.ValueChanged += (s, ev) => { try { this.trackBarContrast.Value = (int)this.numericUpDownContrast.Value; } catch { } };
                }
                if (this.trackBarBrightness != null && this.numericUpDownBrightness != null)
                {
                    this.numericUpDownBrightness.Minimum = new decimal(this.trackBarBrightness.Minimum);
                    this.numericUpDownBrightness.Maximum = new decimal(this.trackBarBrightness.Maximum);
                    if (this.trackBarBrightness.Value < this.trackBarBrightness.Minimum) this.trackBarBrightness.Value = this.trackBarBrightness.Minimum;
                    if (this.trackBarBrightness.Value > this.trackBarBrightness.Maximum) this.trackBarBrightness.Value = this.trackBarBrightness.Maximum;
                    try { this.numericUpDownBrightness.Value = new decimal(this.trackBarBrightness.Value); } catch { }
                    this.trackBarBrightness.Scroll += (s, ev) => { try { this.numericUpDownBrightness.Value = new decimal(this.trackBarBrightness.Value); } catch { } };
                    this.numericUpDownBrightness.ValueChanged += (s, ev) => { try { this.trackBarBrightness.Value = (int)this.numericUpDownBrightness.Value; } catch { } };
                }
            }
            catch { }

            worker_udp = new BackgroundWorker();
            worker_udp.WorkerReportsProgress = true;
            worker_udp.WorkerSupportsCancellation = true;
            worker_udp.DoWork += new DoWorkEventHandler(worker_udp_DoWork);
            worker_udp.ProgressChanged += new ProgressChangedEventHandler(worker_udp_ProgressChanged);

            xoa = xoa_mes;
            yoa = yoa_mes;

            // local ip address
            mmLocalHost = Dns.GetHostName();
            IPAddress[] addresses = Dns.GetHostAddresses(mmLocalHost);
            foreach (IPAddress address in addresses)
            {
                mmLocalIP = address.ToString();
            }

            // VideoInput
            if (cam_maker == Camera_Maker.analog)
            {
                worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.WorkerSupportsCancellation = true;
                worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);

                appTitle = "MT3 analog " + appSettings.ID.ToString();
            }

            // IDS
            if (cam_maker == Camera_Maker.IDS)
            {
                //u32DisplayID = pictureBox1.Handle.ToInt32();                
                appTitle = "MT3IDS " + appSettings.ID.ToString();
            }

            //AVT
            if (cam_maker == Camera_Maker.AVT)
            {
                appTitle = "MT3AVT " + appSettings.ID.ToString();
            }

            //Basler
            if (cam_maker == Camera_Maker.Basler)
            {
                appTitle = "MT3Basler " + appSettings.ID.ToString();
                Text = "MT3BaslerAce";
                /* Register for the events of the image provider needed for proper operation. */
                //m_imageProvider.GrabErrorEvent += new ImageProvider.GrabErrorEventHandler(OnGrabErrorEventCallback);
                //m_imageProvider.DeviceRemovedEvent += new ImageProvider.DeviceRemovedEventHandler(OnDeviceRemovedEventCallback);
                //m_imageProvider.DeviceOpenedEvent += new ImageProvider.DeviceOpenedEventHandler(OnDeviceOpenedEventCallback);
                //m_imageProvider.DeviceClosedEvent += new ImageProvider.DeviceClosedEventHandler(OnDeviceClosedEventCallback);
                //m_imageProvider.GrabbingStartedEvent += new ImageProvider.GrabbingStartedEventHandler(OnGrabbingStartedEventCallback);
                //m_imageProvider.ImageReadyEvent += new ImageProvider.ImageReadyEventHandler(OnImageReadyEventCallback);
                //m_imageProvider.GrabbingStoppedEvent += new ImageProvider.GrabbingStoppedEventHandler(OnGrabbingStoppedEventCallback);
 
                /* Update the list of available devices in the upper left area. */
                //UpdateDeviceList();
            }
            Pid_Data_Send_Init();
            star.init(); // starデータ初期化

            // Fish2 camera model initialization
            fish2 = new AllSkyCamera.FisheyeCameraModel(appSettings.Width, appSettings.Height, appSettings.FocalLength / appSettings.Ccdpx );// (2608, 2608, 2.7 / 0.00345);
            // Load roll/tilt from settings and apply to fish2
            try
            {
                double rolldeg = appSettings.Roll; // Roll angle in degrees
                double tiltXdeg = appSettings.TiltX;
                double tiltYdeg = appSettings.TiltY;

                numericUpDownRoll.Value = (decimal)appSettings.Roll; // Roll angle in degrees
                numericUpDownTiltX.Value = (decimal)appSettings.TiltX;
                numericUpDownTiltY.Value = (decimal)appSettings.TiltY;

                fish2.SetPointingError(tiltXdeg, tiltYdeg, rolldeg);
            }
            catch
            {
                // ignore if settings are unavailable
            }

            // Add a runtime checkbox to toggle Az/Alt grid overlay next to existing controls
            try
            {
                checkBoxGrid = new System.Windows.Forms.CheckBox();
                checkBoxGrid.Text = "Show Az/Alt Grid";
                checkBoxGrid.AutoSize = true;
                checkBoxGrid.Checked = false;
                checkBoxGrid.Location = new System.Drawing.Point(10, 680);
                checkBoxGrid.CheckedChanged += (s, e) => { try { this.Invoke(new Action(() => pictureBox1.Invalidate())); } catch { pictureBox1.Invalidate(); } };
                this.Controls.Add(checkBoxGrid);
            }
            catch { }

            // Initialize numericFocalLength control if present
            try
            {
                if (numericFocalLength != null)
                {
                    numericFocalLength.DecimalPlaces = 2;
                    numericFocalLength.Increment = new decimal(new int[] {1,0,0,131072}); // 0.01 mm
                    numericFocalLength.Minimum = new decimal(new int[] {1,0,0,0});
                    numericFocalLength.Maximum = new decimal(new int[] {1000,0,0,0});
                    numericFocalLength.Value = (decimal)appSettings.FocalLength;
                    numericFocalLength.ValueChanged -= numericFocalLength_ValueChanged;
                    numericFocalLength.ValueChanged += numericFocalLength_ValueChanged;
                }
            }
            catch { }

            string trackerCamHost = @"192.168.1.221";
            int cameraport = 22222;
            string outbasedir = @"D:\img_data\data\";
            MeteorDetection.UsageExample.Init(trackerCamHost, cameraport, outbasedir, fish2);
        }

            

        private void Form1_Load(object sender, EventArgs e)
        {
            this.worker_udp.RunWorkerAsync();

            appTitle = "MT3" + appSettings.Text + " " + appSettings.ID.ToString() + "  " + mmLocalHost + "(" + mmLocalIP + ")";
            this.Text = appTitle;
        }

        //Form起動後１回だけ発生
        private void Form1_Shown(object sender, EventArgs e)
        {
            checkBoxObsAuto_CheckedChanged(sender, e);
            try
            {
                diskspace = cDrive.TotalFreeSpace;
            }
            catch (System.IO.IOException ve)
            {
                richTextBox1.AppendText(ve.ToString());
                logger.Error(ve.ToString());
            }
            timerMTmonSend.Start();

            starttime = Planet.ObsStartTime(DateTime.Now) - DateTime.Today;
            endtime = Planet.ObsEndTime(DateTime.Now) - DateTime.Today;
            string s = string.Format("ObsStart:{0},   ObsEnd:{1}\n", starttime, endtime);
            richTextBox1.AppendText(s);
            logger.Info(s);
            timer_thingspeak_Tick( sender, e);
            buttonMakeDark_Click(sender, e);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //UDP停止
            if (worker_udp.IsBusy)
            {
                worker_udp.CancelAsync();
            }
            // IDS
            if (cam_maker == Camera_Maker.IDS)
            {
                //cam.Exit();
            }
            //AVT
            if (cam_maker == Camera_Maker.AVT)
            {
                //avt_cam_end();
            }
            //Basler
            if (cam_maker == Camera_Maker.Basler)
            {
                //BaslerEnd();
            }

            timeEndPeriod(16);
        }

        #region UDP
        //
        // 別スレッド処理（UDP） //IP 192.168.1.214
        //
        private void worker_udp_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;

            //バインドするローカルポート番号
            int localPort = appSettings.UdpPortRecieve;// mmFsiUdpPortSpCam;// 24410 broadcast
            System.Net.Sockets.UdpClient udpc = null; ;
            try
            {
                udpc = new System.Net.Sockets.UdpClient(localPort);
            }
            catch (Exception ex)
            {
                //匿名デリゲートで表示する
                this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                logger.Error(ex.ToString());
            }

            // ベースブロードバンドポートなら転送
            System.Net.Sockets.UdpClient udpc2 = null; ;
            if (localPort == mmUdpPortBroadCast)
            {
                int localPortSent = mmUdpPortBroadCastSent;
                try
                {
                    udpc2 = new System.Net.Sockets.UdpClient(localPortSent);
                }
                catch (Exception ex)
                {
                    //匿名デリゲートで表示する
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                }
            }

            //文字コードを指定する
            System.Text.Encoding enc = System.Text.Encoding.UTF8;

            string str;
            MOTOR_DATA_KV_SP kmd3 = new MOTOR_DATA_KV_SP();
            int size = Marshal.SizeOf(kmd3);
            KV_DATA kd = new KV_DATA();
            int sizekd = Marshal.SizeOf(kd);

            //データを受信する
            System.Net.IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, localPort);
            while (bw.CancellationPending == false)
            {
                byte[] rcvBytes = udpc.Receive(ref remoteEP);
                if (rcvBytes.Length == sizekd)
                {
                    kd = ToStruct1(rcvBytes);
                    bw.ReportProgress(0, kd);

                    // ベースブロードバンドポートなら転送
                    if (localPort == mmUdpPortBroadCast)
                    {
                        //データを送信するリモートホストとポート番号
                        string remoteHost = "localhost";
                        //string remoteHost = "192.168.1.204";
                        int remotePort = 24441;  // アプリ1
                        udpc2.Send(rcvBytes, rcvBytes.Length, remoteHost, remotePort);

                        remotePort = 24442;  // アプリ2
                        udpc2.Send(rcvBytes, rcvBytes.Length, remoteHost, remotePort);

                        remotePort = 24443;  // アプリ3
                        udpc2.Send(rcvBytes, rcvBytes.Length, remoteHost, remotePort);
                    }
                }
                else if (rcvBytes.Length == size)
                {
                    kmd3 = ToStruct(rcvBytes);
                    if (kmd3.cmd == 1) //mmMove:1
                    {
                        Mode = DETECT;
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveMainTime, RUN });
                        this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveTimeOver, RUN });
                        //保存処理開始
                        if (this.States == RUN)
                        {
                            ImgSaveFlag = TRUE;
                            // 過去データ保存
                            if (appSettings.PreSaveNum > 0)
                            {
                                fifo.Saveflag_true_Last(appSettings.PreSaveNum);  // 1fr=0.2s  -> 5fr=1s 
                            }
                            this.States = SAVE;
                            this.udpkv.kalman_init();
                            pos_mes.init();
                            logger.Info("Save CMD recive:Save start.");

                            string savedir = appSettings.SaveDir + DateTime.Now.ToString("yyyyMMdd") + @"\";
                            // フォルダ (ディレクトリ) が存在しているかどうか確認する
                            if (!System.IO.Directory.Exists(savedir))
                            {
                                System.IO.Directory.CreateDirectory(savedir);
                            }
                            string bg_fn = savedir + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + string.Format("_{00}_bg", appSettings.NoCapDev);
                            SaveAvgImage(bg_fn);
                        }
                    }
                    else if (kmd3.cmd == 90) //mmPidTest:90
                    {
                        Mode = PID_TEST;
                        test_start_id = pid_data.id;
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveMainTime, RUN });
                        this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveTimeOver, RUN });
                        //保存処理開始
                        if (this.States == RUN)
                        {
                            ImgSaveFlag = TRUE;
                            this.States = SAVE;
                        }
                    }
                    else if (kmd3.cmd == 16) //mmLost:16
                    {
                        //Mode = LOST;
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveMainTime, STOP });
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSavePostTime, RUN });
                    }
                    else if (kmd3.cmd == 17) // mmMoveEnd             17  // 位置決め完了
                    {
                        Mode = DETECT_IN;
                    }
                    else if (kmd3.cmd == 18) // mmTruckEnd            18  // 追尾完了
                    {
                        //保存処理終了
                        Mode = LOST;
                        this.Invoke(new dlgTimer(ButtonSaveEnd_Click), new object[] { sender, e });
                        //timerSave.Stop() x;
                        //timerSave_Tick(sender, e) x;
                        //timerSaveTimeOver.Stop() x;
                        //ButtonSaveEnd_Click(sender, e) x;
                    }
                    else if (kmd3.cmd == 20) //mmData  20  // send fish pos data
                    {
                        //匿名デリゲートで表示する
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveMainTime, STOP });
                        //this.Invoke(new dlgSetColor(SetTimer), new object[] { timerSaveMainTime, RUN }); // main timer 延長
                    }

                    str = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + " UDP " + kmd3.cmd.ToString("CMD:00") + " Az:" + kmd3.az + " Alt:" + kmd3.alt + " VAz:" + kmd3.vaz + " VAlt:" + kmd3.valt + "\n";
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, str });
                    logger.Info(str);
                    //bw.ReportProgress(0, kmd3);
                }
                else
                {
                    string rcvMsg = enc.GetString(rcvBytes);
                    str = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "受信したデータ:[" + rcvMsg + "]\n";
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, str });
                    logger.Info(str);
                }

                //str = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "送信元アドレス:{0}/ポート番号:{1}/Size:{2}\n" + remoteEP.Address + "/" + remoteEP.Port + "/" + rcvBytes.Length;
                //this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, str });
            }

            //UDP接続を終了
            udpc.Close();
        }
        //メインスレッドでの処理
        private void worker_udp_ProgressChanged(object sender, ProgressChangedEventArgs e)

        {
            // 画面表示
            //MOTOR_DATA_KV_SP kmd3 = (MOTOR_DATA_KV_SP)e.UserState;
            //string s = string.Format("worker_udp_ProgressChanged:[{0} {1} az:{2} alt:{3}]\n", kmd3.cmd, kmd3.t, kmd3.az, kmd3.alt);
            //  richTextBox1.AppendText(s);
            udpkv.kd = (KV_DATA)e.UserState;
            udpkv.cal_mt3();
            udpkv.cal_mt2();
        }

        static byte[] ToBytes(MOTOR_DATA_KV_SP obj)
        {
            int size = Marshal.SizeOf(typeof(MOTOR_DATA_KV_SP));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            Marshal.FreeHGlobal(ptr);
            return bytes;
        }
        static byte[] ToBytes(FSI_PID_DATA obj)
        {
            int size = Marshal.SizeOf(typeof(FSI_PID_DATA));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            Marshal.FreeHGlobal(ptr);
            return bytes;
        }

        public static MOTOR_DATA_KV_SP ToStruct(byte[] bytes)
        {
            GCHandle gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            MOTOR_DATA_KV_SP result = (MOTOR_DATA_KV_SP)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(MOTOR_DATA_KV_SP));
            gch.Free();
            return result;
        }

        public static KV_DATA ToStruct1(byte[] bytes)
        {
            GCHandle gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            KV_DATA result = (KV_DATA)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(KV_DATA));
            gch.Free();
            return result;
        }


        #endregion

        #region function

        public void ccd_defect_correct(int x, int y)
        {
            Scalar v1;
            //v1 = imgdata.img.At< byte > ( y - 3, x ); // mono8:byte  mono16:ushort
            v1 = imgdata.img.Get<byte>(y - 3, x);
            imgdata.img.Set(y - 1, x, v1);
            v1 = imgdata.img.Get<byte>(y - 2, x);
            imgdata.img.Set(y, x, v1);
            v1 = imgdata.img.Get<byte>(y + 3, x);
            imgdata.img.Set(y + 1, x, v1);

            //v1.Val0 = 256;
            //Cv.Set<double>(imgdata.img, y+1, x, v1);

        }

        //現在の時刻の表示と、タイマーの表示に使用されるデリゲート
        delegate void dlgSetString(object lbl, string text);
        //ボタンのカラー変更に使用されるデリゲート
        delegate void dlgSetColor(object lbl, int state);
        delegate void dlgTimer(object sender, EventArgs e);

        //デリゲートで別スレッドから呼ばれてラベルに現在の時間又は
        //ストップウオッチの時間を表示する
        private void ShowRText(object sender, string str)
        {
            RichTextBox rtb = (RichTextBox)sender; //objectをキャストする
            rtb.AppendText(str);
        }
        private void ShowText(object sender, string str)
        {
            TextBox rtb = (TextBox)sender;　//objectをキャストする
            rtb.Text = str;
        }
        private void ShowLabelText(object sender, string str)
        {
            Label rtb = (Label)sender;　//objectをキャストする
            rtb.Text = str;
        }
        private void SetColor(object sender, int sta)
        {
            Button rtb = (Button)sender;　//objectをキャストする
            if (sta == RUN)
            {
                rtb.BackColor = Color.Red;
            }
            else if (sta == STOP)
            {
                rtb.BackColor = Color.FromKnownColor(KnownColor.Control);
            }
        }
        private void SetTimer(object sender, int sta)
        {
            System.Windows.Forms.Timer tim = (System.Windows.Forms.Timer)sender;　//objectをキャストする
            if (sta == RUN)
            {
                tim.Start();
            }
            else if (sta == STOP)
            {
                tim.Stop();
            }
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // First, handle the case where an exception was thrown.
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
            }
            else if (e.Cancelled)
            {
                // Next, handle the case where the user canceled 
                // the operation.
                // Note that due to a race condition in 
                // the DoWork event handler, the Cancelled
                // flag may not have been set, even though
                // CancelAsync was called.
                this.ObsStart.BackColor = Color.FromKnownColor(KnownColor.Control);
                this.ObsEndButton.BackColor = Color.FromKnownColor(KnownColor.Control);
            }
            this.States = STOP;
        }
        #endregion

        void star_setup()
        {
            double azc = 0;
            double altc = 90;
            double theta = 178.3;
            double fl = 2.670;
            double xc = 960 - 25;
            double yc = 600 + 15;
            int fov = 170; //[deg]    690; //pixel

            // 初期化　CCD上の星位置計算
            DateTime t = new DateTime(2018, 3, 14, 1, 30, 1);
            //star.init(t);
            //star.init_BSC(t, @"S:\satoshi\Lib\bsc5.dat");
            star.init_BSC(t, @"bsc5.dat");
            star.cam.init(azc, altc, theta, fl, 0, 0, 0, xc, yc);
            star.cal_ccd_xy();
            star.cam.EFOV = fov;
        }

        private void ShowButton_Click(object sender, EventArgs e)
        {
            Pid_Data_Send_KV1000_SpCam2((short)frame_id, daz, dalt, 1);
            //uEye_PostSave_settings();
            /*
             Pid_Data_Send_KV1000_SpCam2((short)frame_id, daz, dalt, 1);

            // Obs End test
            ObsEndButton_Click(sender, e);
            timerWaitShutdown.Start();

            //AVT
            /*
            if (cam_maker == Camera_Maker.AVT)
            {
                avt_cam_start();
            }
            */
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void ObsEndButton_Click(object sender, EventArgs e)
        {
            this.States = STOP;
            timerDisplay.Enabled = false;
            this.ObsEndButton.Enabled = false;
            this.ObsEndButton.BackColor = Color.Red;
            this.ObsStart.Enabled = true;
            this.ObsStart.BackColor = Color.FromKnownColor(KnownColor.Control);

            //AVT
            if (cam_maker == Camera_Maker.AVT)
            {
                //avt_cam_end();
            }
            //Basler
            if (cam_maker == Camera_Maker.Basler)
            {
                //Stop(); /* Stops the grabbing of images. */
                //BaslerEnd();
            }
            //PGR
            if (cam_maker == Camera_Maker.PointGreyCamera)
            {
                pgc_cam_stop_flag = 1;
                
                ClosePGRcamera();
            }
            //IDS
            if (cam_maker == Camera_Maker.IDS)
            {
                //if (cam.Acquisition.Stop() == uEye.Defines.Status.SUCCESS)
                {
                }
                //cam.Exit();
            }
            //ImaginSouse
            if (cam_maker == Camera_Maker.ImagingSouce)
            {
                //icImagingControl1.LiveStop();
            }
            //analog
            if (cam_maker == Camera_Maker.analog)
            {
                // BackgroundWorkerを停止.
                if (worker.IsBusy)
                {
                    this.worker.CancelAsync();
                }
            }

            // TrackManager GateRadius UI 初期同期
            try
            {
                if (this.numericUpDownGateRadiusPx != null)
                {
                    // 既存の TrackManager インスタンスがあればその値を反映
                    // フォーム内で TrackManager を保持している場合は field 名を変更してください
                    // ここでは可能性として video pipeline などと連携するケースに配慮し、
                    // フィールド名 'trackManager' を探索して適用します。
                    this.numericUpDownGateRadiusPx.Value = 60; // デフォルト
                    try
                    {
                        // 反映対象のフィールドが存在すれば取得して反映する
                        var fi = this.GetType().GetField("trackManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        if (fi != null)
                        {
                            var tm = fi.GetValue(this) as dynamic;
                            if (tm != null)
                            {
                                try { this.numericUpDownGateRadiusPx.Value = (decimal)tm._cfg.GateRadiusPx; } catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ObsStart_Click(object sender, EventArgs e)
        {
            richTextBox1.AppendText(DateTime.Now.ToString()+ ":Obs. started."+ Environment.NewLine);
            NLogInfo("Obs. started.");

            //PGR
            if (cam_maker == Camera_Maker.PointGreyCamera)
            {
                Task.Run(() => OpenPGRcamera());

                System.Threading.Thread.Sleep(3000);
                checkBox_ExposureAuto.Checked = appSettings.ExposureAuto;
                checkBox_GainAuto.Checked = appSettings.GainAuto;
                try
                {
                    // initialize manual controls to saved settings
                    numericUpDownExposureManual.Value = (decimal)appSettings.Exposure;
                }
                catch { }
                try
                {
                    numericUpDownGain.Value = (decimal)appSettings.Gain;
                }
                catch { }
                try
                {
                    numericUpDownFramerate.Value = (decimal)appSettings.Framerate;
                    // set requested framerate variable shown elsewhere
                    reqFramerate = appSettings.Framerate;
                }
                catch { }

            }
            //AVT
            if (cam_maker == Camera_Maker.AVT)
            {
                //avt_cam_start();
            }
            // Basler
            if (cam_maker == Camera_Maker.Basler)
            {
                //BaslerStart(0);   /* 0: Get a handle for the first device found.  */
                //ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
            }
            //IDS
            if (cam_maker == Camera_Maker.IDS)
            {
                //OpenIDScamera();
                //statusRet = cam.Acquisition.Capture();
                //if (statusRet != uEye.Defines.Status.SUCCESS)
                {
                    string s = "Start Live Video failed. IDS cam.";
                    richTextBox1.AppendText(s);
                    logger.Info(s);
                    //cam.Exit();
                    return;
                }
            }
            //analog
            if (cam_maker == Camera_Maker.analog)
            {
                // BackgroundWorkerを開始
                if (!worker.IsBusy)
                {
                    this.worker.RunWorkerAsync();
                }
            }

            LiveStartTime = DateTime.Now;
            this.States = RUN;
            timerDisplay.Enabled = true;
            this.ObsStart.Enabled = false;
            this.ObsStart.BackColor = Color.Red;
            this.ObsEndButton.Enabled = true;
            this.ObsEndButton.BackColor = Color.FromKnownColor(KnownColor.Control);
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            Save_proc();
        }

        private void Save_proc()
        {
            if (this.States == RUN)
            {
                ImgSaveFlag = TRUE;
                this.States = SAVE;
                this.timerSave.Enabled = true;
                // 過去データ保存
                if (appSettings.PreSaveNum > 0)
                {
                    fifo.Saveflag_true_Last(appSettings.PreSaveNum);  // 1fr=0.2s  -> 5fr=1s 
                }
                logger.Info("Save_proc:Start.");
            }
        }

        private void ButtonSaveEnd_Click(object sender, EventArgs e)
        {
            SaveEnd_proc();
        }
        private void SaveEnd_proc()
        {
            ImgSaveFlag = FALSE;
            this.States = RUN;
            this.timerSave.Enabled = false;
            logger.Info("Save_proc:End.");
        }
        // settingsの作成
        private void buttonMakeDark_Click(object sender, EventArgs e)
        {
            ///SettingsMake();
            //appSettings = SettingsLoad(21);
            //SaveAvgImage();
            star_adaptive_threshold = (int)numericUpDownStarMin.Value; // kenyou  0-5 月、惑星  6:シリウス　7:ベガ

            // Make Mask
            StreamReader sr = new StreamReader(@"mask_data.csv");
            {
                // 末尾まで繰り返す
                while (!sr.EndOfStream)
                {
                    // CSVファイルの一行を読み込む
                    string line = sr.ReadLine();
                    // 読み込んだ一行をカンマ毎に分けて配列に格納する
                    string[] values = line.Split(',');

                    // 配列からリストに格納する [xc,yc,r,OUT/IN]
                    List<string> lists = new List<string>();
                    lists.AddRange(values);

                    int x = Convert.ToInt32(lists[0]);
                    int y = Convert.ToInt32(lists[1]);
                    int r = Convert.ToInt32(lists[2]);
                    int io= Convert.ToInt32(lists[3]) * 255;

                    Cv2.Circle(img_mask, new OpenCvSharp.Point(x,y), r, new Scalar(io),-1);
                    img_mask.SaveImage("img_mask.png");

                    // コンソールに出力する
                    foreach (string list in lists)
                    {
                        System.Console.Write("{0} ", list);
                    }
                    System.Console.WriteLine();
                }
            }
        }
        

        private void numericUpDownLoupeZoom_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // numericUpDown configured with DecimalPlaces=0, so Value maps directly to magnification (e.g. 2 => 2x)
                loupeZoom = (double)numericUpDownLoupeZoom.Value;
            }
            catch { }
        }

        // ボタンハンドラ: 観測動画(.avi)を選択し MeteorDetectionPipeline で処理、半分サイズで表示し検出結果をオーバーレイします。
        private async void buttonDetectFromVideo_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "AVI files|*.avi|All files|*.*" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                string path = ofd.FileName;
                await Task.Run(() => ProcessVideoWithPipeline(path));
            }
        }

        // 動画を開いてフレームごとにパイプラインへ送り、1/2サイズで表示し検出矩形/中心を描画する
        private void ProcessVideoWithPipeline(string path)
        {
            var detector = this.videoDetector ?? (this.videoDetector = new MeteorDetection.MeteorFrameDetector());

            // 雲・月マスクを有効化する。
            // null の場合は MeteorFrameDetector 側で雲マスク処理が無効になるため、
            // 動画検出時は既定値の CloudMaskConfig を明示的に設定する。
            if (detector.CloudMaskConfig == null)
            {
                detector.CloudMaskConfig = new MeteorDetection.CloudMaskConfig();
            }
            detector.CloudMaskUpdateIntervalFrames = Math.Max(1, detector.CloudMaskUpdateIntervalFrames);

            var tracker = new TrackManager();
            
            DetectionRecorder recorder = null;
            try
            {
                string tmpDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detecttmp");
                System.IO.Directory.CreateDirectory(tmpDir);
                recorder = new DetectionRecorder(tmpDir);
            }
            catch { recorder = null; }

            using (var pipeline = new MeteorDetectionPipeline(detector, tracker, recorder, null, this.fish2))
            using (var cap = new VideoCapture(path))
            {
                if (!cap.IsOpened())
                {
                    try { this.BeginInvoke(new Action(() => MessageBox.Show($"Cannot open file: {path}"))); } catch { }
                    return;
                }

                Mat frame = new Mat();
                int videoFrameIndex = 0;
                while (cap.Read(frame) && !frame.Empty())
                {
                    // グレースケールに変換して検出
                    using (var gray = new Mat())
                    {
                        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

                        // マスクを縮小して検出に適用（detector.BinFactor に合わせる）
                        Mat maskSmall = null;
                        int bf = Math.Max(1, detector.BinFactor);
                        var smallSize = new OpenCvSharp.Size(Math.Max(1, frame.Width / bf), Math.Max(1, frame.Height / bf));
                        if (this.img_mask != null)
                        {
                            maskSmall = new Mat();
                            Cv2.Resize(this.img_mask, maskSmall, smallSize, 0, 0, InterpolationFlags.Nearest);
                            if (maskSmall.Type() != MatType.CV_8U)
                            {
                                var tmp = new Mat();
                                Cv2.CvtColor(maskSmall, tmp, ColorConversionCodes.BGR2GRAY);
                                maskSmall.Dispose();
                                maskSmall = tmp;
                            }
                        }

                        // パイプライン処理（内部で Detector->Tracker->Recorder/UDP を行う）
                        pipeline.ProcessFrame(gray, maskSmall, DateTime.UtcNow);

                        // 同じ検出を取得して表示用に使う（オフライン処理なので重複しても可）
                        var detections = detector.Process(gray, maskSmall);

                        // 表示用に1/2サイズへ縮小
                        int w2 = Math.Max(1, frame.Width / 2);
                        int h2 = Math.Max(1, frame.Height / 2);
                        using (var disp = new Mat())
                        {
                            Cv2.Resize(frame, disp, new OpenCvSharp.Size(w2, h2), 0, 0, InterpolationFlags.Area);

                            // 統合マスクの除外領域（固定・星・雲）を半透明の薄赤色で重ねる。
                            if (showMaskOverlay)
                            {
                                using (var validMask = detector.GetCombinedValidMask())
                                using (var maskFull = new Mat())
                                using (var maskDisplay = new Mat())
                                using (var redOverlay = new Mat(disp.Size(), disp.Type(), new Scalar(0, 0, 255)))
                                {
                                    if (validMask != null)
                                    {
                                        Cv2.BitwiseNot(validMask, maskFull);
                                        Cv2.Resize(maskFull, maskDisplay, disp.Size(), 0, 0, InterpolationFlags.Nearest);
                                        using (var tinted = new Mat())
                                        {
                                            Cv2.BitwiseAnd(redOverlay, redOverlay, tinted, maskDisplay);
                                            Cv2.AddWeighted(disp, 0.72, tinted, 0.28, 0, disp);
                                        }
                                    }
                                }
                            }

                            // 検出結果をオーバーレイ描画（座標は1/2にスケール）
                            foreach (var d in detections)
                            {
                                var cx = (int)(d.Center.X * 0.5);
                                var cy = (int)(d.Center.Y * 0.5);
                                var rx = (int)(d.BBox.X * 0.5);
                                var ry = (int)(d.BBox.Y * 0.5);
                                var rw = Math.Max(1, (int)(d.BBox.Width * 0.5));
                                var rh = Math.Max(1, (int)(d.BBox.Height * 0.5));

                                //Cv2.Rectangle(disp, new OpenCvSharp.Rect(rx, ry, rw, rh), new Scalar(0, 255, 0), 2);
                                // 中心点は塗りつぶしなしの赤い円（輪郭のみ）
                                Cv2.Circle(disp, new OpenCvSharp.Point(cx, cy), 4, new Scalar(0, 0, 255), 1);
                                Cv2.PutText(disp, d.PeakValue.ToString("F0"), new OpenCvSharp.Point(cx + 6, cy - 6), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);
                            }

                            // トラッカー結果を描画（履歴ポリライン、推定位置円、ID、速度矢印）
                            try
                            {
                                var tracks = pipeline.Tracker.ActiveTracks;
                                if (tracks != null)
                                {
                                    foreach (var tr in tracks)
                                    {
                                    // 推定位置と速度を取得
                                        var pos = tr.Position;
                                        var vel = tr.VelocityPxPerSec;

                                        // スケール（表示は1/2サイズ）
                                        int tx = (int)(pos.X * 0.5);
                                        int ty = (int)(pos.Y * 0.5);

                                        // 履歴を線で描画
                                        if (tr.History != null && tr.History.Count >= 2)
                                        {
                                            var pts = new System.Collections.Generic.List<OpenCvSharp.Point>();
                                            foreach (var h in tr.History)
                                            {
                                                // History の要素は (DateTime T, OpenCvSharp.Point2f Pos) の値タプルなので null チェック不要
                                                pts.Add(new OpenCvSharp.Point((int)(h.Pos.X * 0.5), (int)(h.Pos.Y * 0.5)));
                                            }
                                            if (pts.Count >= 2) Cv2.Polylines(disp, new[] { pts.ToArray() }, false, new Scalar(255, 200, 0), 2);
                                        }

                                        // 推定位置の円（確定済みは緑、暫定は青）
                                        var col = (tr.Confirmed) ? new Scalar(0, 255, 0) : new Scalar(255, 0, 0);
                                        Cv2.Circle(disp, new OpenCvSharp.Point(tx, ty), 5, col, 2);

                                        // ID を表示
                                        Cv2.PutText(disp, $"ID:{tr.Id}", new OpenCvSharp.Point(tx + 6, ty + 6), HersheyFonts.HersheySimplex, 0.5, col, 1);

                                        // 速度ベクトルを描画（短い矢印）
                                        double vx = vel.X * 0.5 * 0.1; // 画面スケールと表示縮尺の調整
                                        double vy = vel.Y * 0.5 * 0.1;
                                        var endp = new OpenCvSharp.Point((int)(tx + vx), (int)(ty + vy));
                                        Cv2.ArrowedLine(disp, new OpenCvSharp.Point(tx, ty), endp, col, 2);
                                    }
                                }
                            }
                            catch { }

                            // 画面内にフレーム番号と検出数を表示
                            string txtFrame = $"Frame: {videoFrameIndex}";
                            string txtDet = $"Detections: {detections?.Count ?? 0}";
                            var pt1 = new OpenCvSharp.Point(10, 24);
                            var pt2 = new OpenCvSharp.Point(10, 52);
                            // 検出パラメータ表示
                            string txtParams = $"Bin:{detector.BinFactor} K:{detector.ThresholdK:F2} MinTh:{detector.MinThreshold:F0} MinA:{detector.MinAreaPx} Dil:{detector.DilateSize}";
                            var pt3 = new OpenCvSharp.Point(10, 80);
                            // 黒で下書き（アウトライン）
                            Cv2.PutText(disp, txtFrame, pt1, HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 3);
                            Cv2.PutText(disp, txtDet, pt2, HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 3);
                            Cv2.PutText(disp, txtParams, pt3, HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 0), 3);
                            // 白で前景文字
                            Cv2.PutText(disp, txtFrame, pt1, HersheyFonts.HersheySimplex, 0.8, new Scalar(255, 255, 255), 1);
                            Cv2.PutText(disp, txtDet, pt2, HersheyFonts.HersheySimplex, 0.8, new Scalar(255, 255, 255), 1);
                            Cv2.PutText(disp, txtParams, pt3, HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 1);

                            Cv2.ImShow("MeteorDetection - Video", disp);
                            Cv2.WaitKey(1000);
                        }
                        // maskSmall を解放
                        try { maskSmall?.Dispose(); } catch { }
                    }

                    int key = Cv2.WaitKey(1);
                    if (key == 27) break; // ESC で中止
                    videoFrameIndex++;
                }

                try { Cv2.DestroyWindow("MeteorDetection - Video"); } catch { }
            }

            try { recorder?.Dispose(); } catch { }
        }



        private void numericUpDownLoupeSize_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                int v = (int)numericUpDownLoupeSize.Value;
                pictureBoxLoupe.Width = Math.Max(50, Math.Min(600, v));
                pictureBoxLoupe.Height = Math.Max(50, Math.Min(600, v));
            }
            catch { }
        }

        private void InitializeCloudMaskControls()
        {
            if (cloudMaskPanel != null || videoDetector == null) return;

            cloudMaskPanel = new Panel
            {
                Name = "cloudMaskPanel",
                Location = new System.Drawing.Point(260, 890),
                Size = new System.Drawing.Size(355, 235),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(cloudMaskPanel);
            cloudMaskPanel.BringToFront();

            AddCloudMaskControl("Opening", "OpeningSizePx", 0, 99, 15, 0);
            AddCloudMaskControl("ThresholdK", "ThresholdK", 0, 50, 3, 1);
            AddCloudMaskControl("MinThreshold", "MinThreshold", 0, 255, 10, 2);
            AddCloudMaskControl("Closing", "ClosingSizePx", 0, 99, 25, 3);
            AddCloudMaskControl("Margin", "MarginPx", 0, 99, 15, 4);
            AddCloudMaskControl("MinCloudArea", "MinCloudAreaPx", 0, 1000000, 50, 5);
            AddCloudMaskControl("UpdateInterval", "UpdateIntervalFrames", 1, 10000, 15, 6);
        }

        private void AddCloudMaskControl(string caption, string key, decimal min, decimal max, decimal value, int row)
        {
            var label = new Label
            {
                Text = caption,
                Location = new System.Drawing.Point(8, 6 + row * 31),
                Size = new System.Drawing.Size(145, 23)
            };
            var numeric = new NumericUpDown
            {
                Name = "numericCloudMask" + key,
                Location = new System.Drawing.Point(160, 4 + row * 31),
                Size = new System.Drawing.Size(100, 25),
                Minimum = min,
                Maximum = max,
                DecimalPlaces = key == "ThresholdK" || key == "MinThreshold" ? 1 : 0,
                Increment = key == "ThresholdK" || key == "MinThreshold" ? 0.5m : 1m,
                Value = value
            };
            numeric.ValueChanged += CloudMaskParameter_ValueChanged;
            cloudMaskControls[key] = numeric;
            cloudMaskPanel.Controls.Add(label);
            cloudMaskPanel.Controls.Add(numeric);
        }

        private void CloudMaskParameter_ValueChanged(object sender, EventArgs e)
        {
            var n = sender as NumericUpDown;
            if (videoDetector == null || n == null || videoDetector.CloudMaskConfig == null) return;

            var cfg = videoDetector.CloudMaskConfig;
            string key = n.Name.Replace("numericCloudMask", string.Empty);
            double value = (double)n.Value;
            if (key == "OpeningSizePx") cfg.OpeningSizePx = (int)value;
            else if (key == "ThresholdK") cfg.ThresholdK = value;
            else if (key == "MinThreshold") cfg.MinThreshold = value;
            else if (key == "ClosingSizePx") cfg.ClosingSizePx = (int)value;
            else if (key == "MarginPx") cfg.MarginPx = (int)value;
            else if (key == "MinCloudAreaPx") cfg.MinCloudAreaPx = (int)value;
            else if (key == "UpdateIntervalFrames") videoDetector.CloudMaskUpdateIntervalFrames = (int)value;
        }

        // Detector UI イベントハンドラ
        private void numericUpDownDetBin_ValueChanged(object sender, EventArgs e)
        {
            try { if (videoDetector != null) videoDetector.BinFactor = (int)numericUpDownDetBin.Value; } catch { }
        }

        private void numericUpDownDetThreshK_ValueChanged(object sender, EventArgs e)
        {
            try { if (videoDetector != null) videoDetector.ThresholdK = (double)numericUpDownDetThreshK.Value; } catch { }
        }

        private void numericUpDownDetMinTh_ValueChanged(object sender, EventArgs e)
        {
            try { if (videoDetector != null) videoDetector.MinThreshold = (double)numericUpDownDetMinTh.Value; } catch { }
        }

        private void numericUpDownDetMinArea_ValueChanged(object sender, EventArgs e)
        {
            try { if (videoDetector != null) videoDetector.MinAreaPx = (int)numericUpDownDetMinArea.Value; } catch { }
        }

        private void numericUpDownDetDilate_ValueChanged(object sender, EventArgs e)
        {
            try { if (videoDetector != null) videoDetector.DilateSize = (int)numericUpDownDetDilate.Value; } catch { }
        }

        // TrackManager GateRadiusPx を UI から即時変更するハンドラ
        private void numericUpDownGateRadiusPx_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                if (numericUpDownGateRadiusPx == null) return;
                double v = (double)numericUpDownGateRadiusPx.Value;

                // フォームが保持する TrackManager インスタンスを探索して即時反映する
                var fi = this.GetType().GetField("trackManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (fi != null)
                {
                    var tm = fi.GetValue(this);
                    if (tm != null)
                    {
                        // TrackManager は内部で TrackManagerConfig を _cfg フィールドに保持しているため反射で設定
                        try
                        {
                            var cfgFi = tm.GetType().GetField("_cfg", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (cfgFi != null)
                            {
                                var cfg = cfgFi.GetValue(tm);
                                if (cfg != null)
                                {
                                    var gateFi = cfg.GetType().GetField("GateRadiusPx", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                    if (gateFi != null) gateFi.SetValue(cfg, v);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void numericFocalLength_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // numericFocalLength is in mm. Update appSettings and fish2 accordingly.
                double fl_mm = (double)numericFocalLength.Value;
                appSettings.FocalLength = fl_mm;
                // Update fish2 focal length in px/rad: focal(mm) / ccd_px(mm)
                if (fish2 != null)
                {
                    try
                    {
                        double f_px = fl_mm / appSettings.Ccdpx;
                        fish2.FocalLengthPx = f_px;
                        // Mark any cached star positions to be recalculated
                        lastStarCalc = DateTime.MinValue;
                    }
                    catch { }
                }
            }
            catch { }
        }


        // 保険: コンストラクタ内の波括弧のバランスを保つためのノート（編集で波括弧を削除した際の保険）


        #region TimerTick
        //
        // Timer Tick
        private void timerSaveTimeOver_Tick(object sender, EventArgs e)
        {
            timerSaveTimeOver.Stop();
            timerSavePost.Stop();
            Mode = LOST;
            if (pgr_post_save)
            {
                ////pgr_Normal_settings();
                //uEye_Normal_settings();
                pgr_post_save = false;
            }
            ButtonSaveEnd_Click(sender, e);
        }

        private void timerSave_Tick(object sender, EventArgs e)
        {
            Mode = LOST;
            timerSave.Stop();

            if (appSettings.PostSaveProcess)
            {
                //　カメラ毎の処理
                if (!pgr_post_save)
                {
                    ////pgr_PostSave_settings();
                    //uEye_PostSave_settings();
                    timerSavePost.Start();
                    pgr_post_save = true;
                    return;
                }
            }

            pgr_post_save = false;
            timerSaveTimeOver.Stop();
            ButtonSaveEnd_Click(sender, e);
        }
        private void timerSavePostTime_Tick(object sender, EventArgs e)
        {
            Mode = LOST;
            timerSaveTimeOver.Stop();
            timerSavePost.Stop();
            ////pgr_Normal_settings();
            //uEye_Normal_settings();
            pgr_post_save = false;
            ButtonSaveEnd_Click(sender, e);
        }

        //       private void timerSaveMainTime_Tick(object sender, EventArgs e)
        //       {
        //           timerSavePost.Stop();
        //       }

 
        private void timerObsOnOff_Tick(object sender, EventArgs e)
        {
            try
            {
                diskspace = cDrive.TotalFreeSpace;
            }
            catch (System.IO.IOException ve)
            {
                //richTextBox1.AppendText(ve.ToString());
                logger.Error(ve.ToString());
            }
            MTmon_Data_Send(sender);
 
            TimeSpan nowtime = DateTime.Now - DateTime.Today;
            //TimeSpan endtime = new TimeSpan(7, 0, 0);
            //TimeSpan starttime = new TimeSpan(16,30, 0);


            if (nowtime.CompareTo(endtime) >= 0 && nowtime.CompareTo(starttime) <= 0)
            {
                // DayTime
                if (this.States == RUN && checkBoxObsAuto.Checked)
                {
                    ObsEndButton_Click(sender, e);
                    timerWaitShutdown.Start();
                }
            }
            else
            {
                //NightTime
                if (this.States == STOP && checkBoxObsAuto.Checked)
                {
                    ObsStart_Click(sender, e);
                }
            }
            // Star display for Fish2
            if (appSettings.NoCapDev == 1)
            {
                star_visible_num = cal_star_disp_pos(appSettings.Theta, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy); // fish2 
            }
        }

        private void timerWaitShutdown_Tick(object sender, EventArgs e)
        {
            shutdown(sender, e);
        }

        private void timerMTmonSend_Tick(object sender, EventArgs e)
        {
            MTmon_Data_Send(sender);
        }

        private void timer1min_Tick(object sender, EventArgs e)
        {
 
            //　PGR ポスト処理不具合暫定対応用
            if (States == RUN && appSettings.PostSaveProcess)
            {
                //if (!check_uEye_normal_mode()) uEye_Normal_settings();

                if (pgr_post_save == true && !timerSavePost.Enabled)
                {
                    ////pgr_Normal_settings();
                    //uEye_Normal_settings();
                    pgr_post_save = false;
                }
               /* else if (dFramerate < 2.0 && !timerSavePost.Enabled)
                {
                    ObsEndButton_Click(sender, e);
                }
                */
            }
        }

        private void checkBoxGainBoost_CheckedChanged(object sender, EventArgs e)
        {
            // IDS
            if (cam_maker == Camera_Maker.IDS)
            {
                //cam.Gain.Hardware.Boost.SetEnable(checkBoxDispAvg.Checked);
            }
        }

        private void timerAutoStarData_Tick(object sender, EventArgs e)
        {
            if (checkBox_DispMode.Checked)
            {
                buttonMove_Click(sender, e);
            }
        }

        #endregion


        private void checkBoxObsAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxObsAuto.Checked)
            {
                this.ObsStart.Enabled = false;
                this.ObsEndButton.Enabled = false;
            }
            else
            {
                if (States == RUN)
                {
                    this.ObsStart.Enabled = false;
                    this.ObsEndButton.Enabled = true;
                }
                if (States == SAVE)
                {
                    this.ObsStart.Enabled = false;
                    this.ObsEndButton.Enabled = true;
                }
                if (States == STOP)
                {
                    this.ObsStart.Enabled = true;
                    this.ObsEndButton.Enabled = false;
                }
            }
        }

        /// <summary>
        /// システムシャットダウン
        /// </summary>
        /// <param name="capacity">シャットダウン</param>
        private void shutdown(object sender, EventArgs e)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = "shutdown.exe";
            //コマンドラインを指定
            psi.Arguments = "-s -f";
            //ウィンドウを表示しないようにする（こうしても表示される）
            psi.CreateNoWindow = true;
            //起動
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
        }


        /// <summary>
        /// 回転座標計算ルーチン
        /// IN:中心座標 CvPoint2D64f
        ///    半径　double
        ///    回転角　double
        /// OUT:目標座標 CvPoint2D64f
        /// </summary>
        /// <param name="capacity">画像表示用回転座標計算ルーチン</param>
        public Point2d Rotation(Point2d xy, double r, double theta)
        {
            double sinth = 0, costh = r;
            Point2d ans = new Point2d();

            if (appSettings.CamPlatform == Platform.MT2 && udpkv.mt2mode == udpkv.mmWest)
            {
                sinth = Math.Sin(-(theta + 90) * Math.PI / 180.0);
                costh = Math.Cos(-(theta + 90) * Math.PI / 180.0);
                ans.Y = -costh * r;
                ans.X = +sinth * r;
            }
            if (appSettings.CamPlatform == Platform.MT2 && udpkv.mt2mode == udpkv.mmEast)
            {
                sinth = Math.Sin(-(theta + 90) * Math.PI / 180.0);
                costh = Math.Cos(-(theta + 90) * Math.PI / 180.0);
                ans.Y = -costh * r;
                ans.X = +sinth * r;
            }

            return (ans + xy);
        }
        /// <summary>
        /// 画像表示ルーチン
        /// </summary>
        /// <param name="capacity">画像表示用タイマールーチン</param>
        private void timerDisplay_Tick(object sender, EventArgs e)
        {
            if (this.States == STOP) return;

            sw.Reset(); sw.Start();
            //int id = System.Threading.Thread.CurrentThread.ManagedThreadId; Console.WriteLine("timerDisplay_Tick ThreadID : " + id);

            //OpenCV　表示ルーチン
            if (imgdata_static.img != null)
            {
                //Cv2.ImShow("test", imgdata_static.img);//Cv2.WaitKey();
                // カラー判定
                if (cam_color == Camera_Color.mono)
                {
                    if (checkBoxDispAvg.Checked == true)
                    {
                        // 移動平均画像の表示
                        double scale = 2.0;
                        Cv2.ConvertScaleAbs(imgAvg, img_dmk, scale);
                        //Cv2.ConvertScaleAbs(fifo.backgroundImageF(), img_dmk, scale);
                        Cv2.CvtColor(img_dmk, img_dmk3, ColorConversionCodes.GRAY2BGR);
                    }
                    else
                    {
                        //Cv2.CvtColor(imgdata_static.img, img_dmk3, ColorConversionCodes.GRAY2BGR);
                        Cv2.CvtColor(fifo.FirstImage(), img_dmk3, ColorConversionCodes.GRAY2BGR);
                    }
                }
                else
                {
                    Cv2.CvtColor(imgdata_static.img, img_dmk3, ColorConversionCodes.BayerGB2BGR); //ColorConversion.BayerGbToBgr);
                }

                // Mask display for Fish2
                if (appSettings.NoCapDev == 1)
                {
                    // Mask 描画
                    var imgtmp = Cv2.Split(img_dmk3);
                    var imgtmp2 = ~img_mask; //反転

                    try
                    {
                        Cv2.Add(imgtmp[2], imgtmp2 / 4, imgtmp[2]);//赤チャンネルにマスクを描画 '// imgtmp: Mat[]、imgtmp2: Mat を前提
                        Cv2.Merge(imgtmp, img_dmk3);
                    }
                    catch (Exception ex)
                    {
                        //dst?.Dispose();
                        logger?.Error("Cv2.Add failed: " + ex.ToString());
                        // 失敗時は元のチャネルを保持して描画を継続
                    }
                    
                    imgtmp2.Dispose();
                    imgtmp[0].Dispose();
                    imgtmp[1].Dispose();
                    imgtmp[2].Dispose();
                }
                double k0 = 4.0;
                double k1 = 1.3333/k0; //4deg 
                double k2 = 0.3333/k0; //直径1deg
                double roa = appSettings.Roa;

                OpenCvSharp.Point OCPoint = new OpenCvSharp.Point(appSettings.Xoa, appSettings.Yoa);
                Cv2.Circle(img_dmk3, OCPoint, (int)roa, new Scalar(200, 0, 255));

                OpenCvSharp.Point2d Point1;
                OpenCvSharp.Point2d Point2;
                String str;

                if (udpkv.mt2mode == udpkv.mmWest)
                {
                    Point1 = Rotation(OCPoint, k1 * roa, theta_c);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));
                    Cv2.Circle(img_dmk3, (OpenCvSharp.Point)Point1, (int)k2, new Scalar(0, 255, 0));       // Arrow

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 90);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 90);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 180);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 180);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 270);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 270);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 105, 230));

                    str = String.Format("ID:{4,7:D1} W: dAz({5,6:F1},{6,6:F1}) dPix({0,6:F1},{1,6:F1})({2,6:F0})({3,0:00}), th:{7,6:F1}", gx, gy, max_val, max_label, frame_id, daz, dalt, theta_c);
                }
                else
                {
                    Point1 = Rotation(OCPoint, k1 * roa, theta_c);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));
                    //Cv.Circle(img_dmk3, Point1, 5, new Scalar(0, 255, 0));       // Arrow

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 90);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 90);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 105, 230));
                    //Cv.Line(img_dmk3, Point1, Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 180);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 180);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));
                    Cv2.Circle(img_dmk3, (OpenCvSharp.Point)Point1, (int)k2, new Scalar(0, 255, 0));       // Arrow

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 270);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 270);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));
                    //Cv.Line(img_dmk3, Point1, Point2, new Scalar(230, 105, 0));

                    str = String.Format("ID:{4,7:D1} E: dAz({5,6:F1},{6,6:F1}) dPix({0,6:F1},{1,6:F1})({2,6:F0})({3,0:00}), th:{7,6:F1}", gx, gy, max_val, max_label, frame_id, daz, dalt, theta_c);
                }
                if (img_dmk3.Width >= 1600)
                {
                    img_dmk3.PutText(str, new OpenCvSharp.Point(24, 48), HersheyFonts.HersheyComplex,2.0, new Scalar(0, 150, 250));
                } else
                {
                    img_dmk3.PutText(str, new OpenCvSharp.Point(6, 24), HersheyFonts.HersheyComplex,1.0, new Scalar(0, 150, 250));
                }
                img_dmk3.Circle(new OpenCvSharp.Point((int)Math.Round(gx), (int)Math.Round(gy)), (int)(roa*max_val/1000), new Scalar(0, 100, 255));
                img_dmk3.Circle(new OpenCvSharp.Point((int)Math.Round(gx), (int)Math.Round(gy)), (int)(10), new Scalar(0, 100, 255));
                //cvwin.Image = imgAvg;

                // Star display for Fish2: オーバーレイ用に描画情報を収集し、別ビットマップで重ねる
                if (appSettings.NoCapDev == 1)
                {
                    // appendText removed to avoid frequent file I/O in display loop
                    double cx, cy, r_mag;
                    //int cx, cy, r_mag;
                    int r_base = 10;
                    int r_p = 4;
                    int star_disp_count = 0;

                    // 星位置は頻繁に変化しないため、一定間隔でのみ再計算する
                    bool needCalc = (DateTime.Now - lastStarCalc).TotalSeconds >= starCalcIntervalSec || overlayBitmap == null;
                    if (needCalc)
                    {
                        var starPoints = new List<Tuple<int,int,int>>(); // px,py,radius

                        for (int i = 0; i < star.Count; ++i)
                        {
                            //get_star_disp_pos_fish2(i, 0, 0, appSettings.Theta, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy, out cx, out cy, out r_mag);//appSettings.Theta, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpy
                            get_star_CCD_pos(i, out cx, out cy, out r_mag);
                            if (get_star_pos_alt(i) > 0.0)
                            {
                                int calcMag = (int)(r_base - r_p * r_mag);
                                int radius = Math.Max(2, Math.Abs(2 * calcMag));

                                int px = (int)Math.Round(appSettings.Xoa + cx);
                                int py = (int)Math.Round(appSettings.Yoa + cy);

                                if (px >= 0 && py >= 0 && px < img_dmk3.Width && py < img_dmk3.Height)
                                {
                                    starPoints.Add(new Tuple<int,int,int>(px, py, radius));
                                    star_disp_count++;
                                }
                            }
                        }

                        // overlayBitmap を新規作成して描画
                        if (starPoints.Count > 0)
                        {
                            Bitmap overlay = new Bitmap(img_dmk3.Width, img_dmk3.Height, PixelFormat.Format32bppArgb);
                            using (Graphics g = Graphics.FromImage(overlay))
                            {
                                g.Clear(Color.Transparent);
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                // 塗りつぶしなしで輪郭のみ描画。明るさに応じて線幅を変える。
                                foreach (var t in starPoints)
                                {
                                    int cxp = t.Item1;
                                    int cyp = t.Item2;
                                    int r = t.Item3;
                                    int penWidth = 1; //Math.Max(1, r / 3);
                                    using (Pen p = new Pen(Color.FromArgb(220, 0, 255, 0), penWidth))
                                    {
                                        p.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;
                                        g.DrawEllipse(p, cxp - r, cyp - r, r * 2, r * 2);
                                    }
                                }
                            }

                            lock (overlayLock)
                            {
                                var old = overlayBitmap;
                                overlayBitmap = overlay;
                                try { old?.Dispose(); } catch { }
                            }

                            // PictureBox を非同期で再描画（UI スレッドをブロックしない）
                            try
                            {
                                if (this.IsHandleCreated)
                                {
                                    this.BeginInvoke(new Action(() => pictureBox1.Invalidate()));
                                }
                                else
                                {
                                    pictureBox1.Invalidate();
                                }
                            }
                            catch { pictureBox1.Invalidate(); }
                        }

                        // 更新時刻を記録
                        lastStarCalc = DateTime.Now;
                    }

                    // file logging removed from display loop
                    //label_mask.Text = star_disp_count.ToString() + "( " + star_visible_num.ToString() + " )";
                }

                try
                {
                    //Cv2.ImShow("PB test", img_dmk3);//Cv2.WaitKey();
                    //Cv2.ImShow("img-avg", imgAvg.PyrDown().PyrDown());
                    //Cv2.ImShow("img-avg", img_dmk3.PyrDown().PyrDown());
                    // UI スレッドで画像を更新し、古い Image を破棄して GDI リソースを確保
                    // Apply brightness/contrast from UI controls before converting to Bitmap
                    var bmp = (Bitmap)null;
                    try
                    {
                        double alpha = 1.0;
                        double beta = 0.0;
                        try
                        {
                            if (trackBarContrast != null) alpha = (double)trackBarContrast.Value / 100.0;
                            if (trackBarBrightness != null) beta = (double)trackBarBrightness.Value;
                        }
                        catch { }

                        using (var img_disp = new Mat())
                        {
                            try { Cv2.ConvertScaleAbs(img_dmk3, img_disp, alpha, beta); } catch { img_disp.Dispose(); throw; }
                            bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(img_disp);
                        }
                    }
                    catch
                    {
                        // Fallback: convert original mat
                        try { bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(img_dmk3); } catch { bmp = null; }
                    }
                    try
                    {
                        if (this.IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                var old = pictureBox1.Image;
                                pictureBox1.Image = bmp;
                                try { old?.Dispose(); } catch { }
                            }));
                        }
                        else
                        {
                            var old = pictureBox1.Image;
                            pictureBox1.Image = bmp;
                            try { old?.Dispose(); } catch { }
                        }
                    }
                    catch
                    {
                        try { var old = pictureBox1.Image; pictureBox1.Image = bmp; try { old?.Dispose(); } catch { } } catch { }
                    }

                    // Also request loupe update on UI thread so loupe follows latest main image
                    try
                    {
                        if (this.IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    bool loupeOn = false;
                                    try { loupeOn = checkBoxLoupe.Checked; } catch { loupeOn = false; }
                                    if (!loupeOn || pictureBox1.Image == null) return;
                                    var target = loupeCenterInitialized ? loupeCenter : new System.Drawing.Point(pictureBox1.ClientSize.Width / 2, pictureBox1.ClientSize.Height / 2);
                                    UpdateLoupe(target);
                                }
                                catch { }
                            }));
                        }
                    }
                    catch { }

                    //int fid = frame_id % 16;
                    //String filename = "pictureboxImg-" + fid + ".jpg";
                    //img_dmk3.SaveImage(filename);
                }
                catch (System.ArgumentException ex_a)
                {
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, frame_id.ToString() });
                    logger.Error(ex_a.Message);
                    return;
                }
                catch (System.Exception ex)
                {
                    //すべての例外をキャッチする
                    //例外の説明を表示する
                    //匿名デリゲートで表示する
                    this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                    logger.Error(ex.Message);
                    System.Console.WriteLine(ex.Message);
                    return;
                }
            }

            sw.Stop();
            long microseconds = sw.ElapsedTicks / (System.Diagnostics.Stopwatch.Frequency / (1000L * 1000L));
            //Console.WriteLine("timer_disp() : " + frame_id.ToString() + ": (" + sw.ElapsedMilliseconds.ToString() + ")ms " + microseconds);


            string s = null;
            if (appSettings.CamPlatform == Platform.MT2)
            {
                //string s = string.Format("KV:[x2:{0:D6} y2:{1:D6} x2v:{2:D5} y2v:{3:D5} {4} {5}]\n", udpkv.x2pos, udpkv.y2pos, udpkv.x2v, udpkv.y2v, udpkv.binStr_status, udpkv.binStr_request);
                s = string.Format("KV:[x1:{0:D6} y1:{1:D6} Az1:{2,6:F1} Alt1:{3,6:F1}]\n", udpkv.xpos, udpkv.ypos, udpkv.az1_c, udpkv.alt1_c);
            }
            if (appSettings.CamPlatform == Platform.MT3 || appSettings.CamPlatform == Platform.Fish2)
            {
                s = string.Format("KV:[x2:{0:D6} y2:{1:D6} Az2:{2,6:F1} Alt2:{3,6:F1}]\n", udpkv.x2pos, udpkv.y2pos, udpkv.az2_c, udpkv.alt2_c);
            }
            label_X2Y2.Text = s;

            //       label_ID.Text = max_label.ToString("00000");
            //this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, id.ToString() });
            // Status表示
            //this.Invoke(new dlgSetString(ShowLabelText), new object[] { label_X2Y2, String.Format("({0},{1}", udpkv.az2_c, udpkv.alt2_c) });

            //long frame_timestamp=0;
            //double dFramerate = 0; // Frame rate[fr/s]
            //double dExpo = 0; // Exposure[us]
            //long igain = 0; //Gain
            // Error rate
            long frame_total = 0, frame_error = 0;
            long frame_underrun = 0, frame_shoved = 0, frame_dropped = 0;
            double err_rate = 0;

            // IDS
            if (cam_maker == Camera_Maker.IDS)
            {
             //   cam.Timing.Framerate.GetCurrentFps(out dFramerate); //IDS
             //   statusRet = cam.Timing.Exposure.Get(out dExpo);//[ms]
             //   dExpo *= 1000; // [us]
             //   int ig;
             //   cam.Gain.Hardware.Scaled.GetMaster(out ig);
             //   igain = ig;
             //   uEye.Types.CaptureStatus captureStatus;
             //   cam.Information.GetCaptureStatus(out captureStatus); //IDS ueye
             //   frame_error = (long)captureStatus.Total;
             //   frame_total = (long)(imageInfo.FrameNumber - ueye_frame_number);
            }
            // PGR
            if (cam_maker == Camera_Maker.PointGreyCamera)
            {
                dFramerate = pgr_framerate; //pgr_frame_rate; // frame rate [fps]
                dExpo = pgr_image_expo_us; // [us]
                igain = (long) pgr_image_gain;
                //uEye.Types.CaptureStatus captureStatus;
                //cam.Information.GetCaptureStatus(out captureStatus); //IDS ueye
                //frame_error = (long)captureStatus.Total;
                frame_total = pgr_image_frame_count;
                //reqFramerate = pgr_getFrameRate();
                frame_underrun = pgr_StreamFailedBufferCount;
                //label_frame_rate.Text = pgr_BusSpeed().ToString() + " " + ((pgr_Temperature(pgr_cam) - 2732) / 10.0).ToString();
            }
            // Basler
            if (cam_maker == Camera_Maker.Basler)
            {
             //   dFramerate = m_imageProvider.GetFrameRate(); // Basler
             //   dExpo = GetExposureTime();
             //   igain = GetGain();
             //   frame_timestamp = m_imageProvider.GetTimestamp();
             //   frame_total = m_imageProvider.Get_Statistic_Total_Buffer_Count();
             //   frame_underrun = m_imageProvider.Get_Statistic_feature("Statistic_Buffer_Underrun_Count");
             //   frame_error = frame_underrun + m_imageProvider.Get_Statistic_feature("Statistic_Failed_Buffer_Count");
             //   //frame_dropped = m_imageProvider.Get_Statistic_feature("Statistic_Total_Packet_Count");
            }
            // AVT
            if (cam_maker == Camera_Maker.AVT)
            {
                try
                {
             //        dFramerate = StatFrameRate(); //AVT
             //       dExpo = ExposureTimeAbs();
                }
                catch
                {
                    MessageBox.Show("error1");
                }
             //   igain = GainRaw();
             //   frame_total = StatFrameDelivered();
             //   frame_underrun = StatFrameUnderrun();// AVT
             //   frame_shoved = StatFrameShoved();
             //   frame_dropped = StatFrameDropped();
             //   frame_error = frame_underrun + frame_dropped;
            }

            toolStripStatusLabelFramerate.Text = "Fps: " + dFramerate.ToString("000.0") + " " + reqFramerate.ToString("000.0");
            toolStripStatusLabelExposure.Text = "Expo: " + (dExpo / 1000.0).ToString("00.00") + "[ms]";
            toolStripStatusLabelGain.Text = "Gain: " + igain.ToString("00");
            toolStripStatusLabelFailed.Text = "Failed U:" + frame_underrun.ToString("0000") + " S:" + frame_shoved.ToString("0000") + " D:" + frame_dropped.ToString("0000");
            //toolStripStatusLabelTemp.Text = "Temp: " + pgr_temparature.ToString("00.0")+"℃  KVAz,Alt:"+udpkv.kvaz.ToString("000") +","+udpkv.kvalt.ToString("00") ;
            toolStripStatusLabelTemp.Text = "Temp: " + pgr_temparature.ToString("00.0") + "℃  Az,Alt:" + udpkv.az2_c.ToString("000") + "," + udpkv.alt2_c.ToString("00");

            //label_frame_rate.Text = pgr_BusSpeed().ToString();

            //double err_rate = 100.0 * (frame_total / (double)id);
            if (frame_total > 0)
            {
                err_rate = 100.0 * (frame_error / (double)frame_total);
            }
            toolStripStatusLabelID.Text = "Frames: " + frame_total.ToString("0000") + " " + frame_error.ToString("0000") + " " + err_rate.ToString("00.00");// +"TS:" + timestamp;

            if (this.States == SAVE)
            {
                this.buttonSave.BackColor = Color.Red;
                this.buttonSave.Enabled = false;
                this.ButtonSaveEnd.Enabled = true;
            }
            if (this.States == RUN)
            {
                this.buttonSave.BackColor = Color.FromKnownColor(KnownColor.Control);
                this.buttonSave.Enabled = true;
                this.ButtonSaveEnd.Enabled = false;
                this.ObsStart.BackColor = Color.Red;
                if (!checkBoxObsAuto.Checked)
                {
                    this.ObsStart.Enabled = false;
                    this.ObsEndButton.Enabled = true;
                }
            }
            if (this.States == STOP)
            {
                this.buttonSave.BackColor = Color.FromKnownColor(KnownColor.Control);
                this.buttonSave.Enabled = false;
                this.ButtonSaveEnd.Enabled = false;
                this.ObsStart.BackColor = Color.FromKnownColor(KnownColor.Control);
                this.ObsStart.Enabled = true;
                this.ObsEndButton.Enabled = false;
            }
        }

        /// <summary>
        /// FIFO pushルーチン
        /// imgdata.img　は　すでにセット済み
        /// </summary>
        private void imgdata_push_FIFO()
        {
            // 文字入れ
            //String str = String.Format("ID:{0,6:D1} ", imgdata.id) + imgdata.t.ToString("yyyyMMdd_HHmmss_fff") + String.Format(" ({0,6:F1},{1,6:F1})({2,6:F0})({3,0:00}), th:{7,6:F1}", gx, gy, max_val, max_label, frame_id, daz, dalt, theta_c);
            //img_dmk.PutText(str, new CvPoint(10, 460), font, new Scalar(255, 100, 100));

            //try
            //{
            //Cv.Sub(img_dmk, img_dark8, imgdata.img); // dark減算
            //Cv.Copy(img_dmk, imgdata.img);
            // cam.Information.GetImageInfo(s32MemID, out imageInfo);
            imgdata.id = (int)frame_id;     // (int)imageInfo.FrameNumber;
            imgdata.t = DateTime.Now; //imageInfo.TimestampSystem;   //  LiveStartTime.AddSeconds(CurrentBuffer.SampleEndTime);
            imgdata.ImgSaveFlag = !(ImgSaveFlag != 0); //int->bool変換
            //statusRet = cam.Timing.Exposure.Get(out exp);
            imgdata.gx = gx;
            imgdata.gy = gy;
            imgdata.kgx = kgx;
            imgdata.kgy = kgy;
            imgdata.kvx = kvx;
            imgdata.kvy = kvy;
            imgdata.vmax = max_val;
            imgdata.blobs = blobs;
            imgdata.udpkv1 = (Udp_kv)udpkv.Clone();
            imgdata.az = az;
            imgdata.alt = alt;
            imgdata.vaz = vaz;
            imgdata.valt = valt;
            if (fifo.Count == appSettings.FifoMaxFrame - 1) fifo.EraseLast();
            fifo.InsertFirst(ref imgdata);
            /*}
            catch (Exception ex)
            {
                //匿名デリゲートで表示する
                this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }*/
            double alfa = 0.05;
            if (frame_id % 4 == 0) // mabiki
            {
                //Cv2.RunningAvg(imgdata.img, imgAvg, 0.05); // 6ms
                Cv2.AccumulateWeighted(imgdata.img, imgAvg, alfa, null);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                // Map mouse coords in PictureBox to image pixel coords
                if (pictureBox1.Image == null) return;
                Rectangle imgRect = GetPictureBoxImageRect(pictureBox1);
                if (imgRect.IsEmpty) return;
                double scaleX = (double)pictureBox1.Image.Width / imgRect.Width;
                double scaleY = (double)pictureBox1.Image.Height / imgRect.Height;
                int ix = (int)((e.X - imgRect.X) * scaleX);
                int iy = (int)((e.Y - imgRect.Y) * scaleY);

                // Add to a list of clicked pixel positions (create if needed)
                try
                {
                    if (clickedPixels == null) clickedPixels = new System.Collections.Generic.List<System.Drawing.Point>();
                }
                catch { }
                try { clickedPixels.Add(new System.Drawing.Point(ix, iy)); } catch { }

                // Convert to horizontal coordinates using fish2 (Pixel -> Az/Alt)
                string outstr = "";
                try
                {
                    double azDeg, altDeg;
                    if (fish2 != null && fish2.PixelToHorizontal(ix, iy, out azDeg, out altDeg))
                    {
                        outstr = string.Format("Pixel=({0},{1}) => Az={2:F3}°, Alt={3:F3}°\n", ix, iy, azDeg, altDeg);
                    }
                    else
                    {
                        outstr = string.Format("Pixel=({0},{1}) => Az/Alt out of range\n", ix, iy);
                    }
                }
                catch (Exception ex)
                {
                    outstr = string.Format("Pixel=({0},{1}) => conversion error: {2}\n", ix, iy, ex.Message);
                }

                this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, outstr });
            }
            catch (Exception ex)
            {
                try { this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() }); } catch { }
            }
        }

        // PictureBox の表示領域取得（SizeMode = Zoom を考慮）
        private Rectangle GetPictureBoxImageRect(PictureBox pb)
        {
            if (pb.Image == null) return Rectangle.Empty;
            int imgW = pb.Image.Width;
            int imgH = pb.Image.Height;
            int pbW = pb.ClientSize.Width;
            int pbH = pb.ClientSize.Height;
            float imgRatio = (float)imgW / imgH;
            float pbRatio = (float)pbW / pbH;
            int drawW, drawH;
            if (imgRatio > pbRatio)
            {
                drawW = pbW;
                drawH = (int)(pbW / imgRatio);
            }
            else
            {
                drawH = pbH;
                drawW = (int)(pbH * imgRatio);
            }
            int x = (pbW - drawW) / 2;
            int y = (pbH - drawH) / 2;
            return new Rectangle(x, y, drawW, drawH);
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            // オーバーレイがあれば描画する
            Bitmap overlay = null;
            lock (overlayLock)
            {
                if (overlayBitmap != null) overlay = (Bitmap)overlayBitmap.Clone();
            }
            if (overlay != null)
            {
                try
                {
                    var rect = GetPictureBoxImageRect(pictureBox1);
                    if (!rect.IsEmpty)
                    {
                        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        e.Graphics.DrawImage(overlay, rect);

                        // Draw Az/Alt grid if enabled
                        bool drawGrid = false;
                        try { if (checkBoxGrid != null) drawGrid = checkBoxGrid.Checked; } catch { drawGrid = false; }
                        if (drawGrid && fish2 != null)
                        {
                            try
                            {
                                // Draw azimuth circles every 30 degrees and altitude lines every 15 degrees
                                using (Pen pen = new Pen(Color.FromArgb(160, 255, 255, 0), 1))
                                {
                                    pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;
                                    // For each altitude (0..90 step 15): draw circle of constant altitude
                                    for (int alt = 0; alt <= 90; alt += 15)
                                    {
                                        // sample many azimuths to get circle points
                                        var pts = new System.Collections.Generic.List<System.Drawing.PointF>();
                                        for (int a = 0; a < 360; a += 5)
                                        {
                                            double xpx, ypx;
                                            if (fish2.HorizontalToPixel(a, alt, out xpx, out ypx))
                                            {
                                                // Map image pixel to pictureBox coords
                                                double px = rect.X + (xpx / pictureBox1.Image.Width) * rect.Width;
                                                double py = rect.Y + (ypx / pictureBox1.Image.Height) * rect.Height;
                                                pts.Add(new System.Drawing.PointF((float)px, (float)py));
                                            }
                                        }
                                        if (pts.Count > 1)
                                        {
                                            e.Graphics.DrawLines(pen, pts.ToArray());
                                        }
                                    }

                                    // Azimuth radial lines every 30 degrees
                                    for (int a = 0; a < 360; a += 30)
                                    {
                                        // draw line from center to edge: sample altitudes from 0 to Max
                                        var pts = new System.Collections.Generic.List<System.Drawing.PointF>();
                                        for (int alt = 0; alt <= 90; alt += 2)
                                        {
                                            double xpx, ypx;
                                            if (fish2.HorizontalToPixel(a, alt, out xpx, out ypx))
                                            {
                                                double px = rect.X + (xpx / pictureBox1.Image.Width) * rect.Width;
                                                double py = rect.Y + (ypx / pictureBox1.Image.Height) * rect.Height;
                                                pts.Add(new System.Drawing.PointF((float)px, (float)py));
                                            }
                                        }
                                        if (pts.Count > 1)
                                        {
                                            e.Graphics.DrawLines(pen, pts.ToArray());
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                finally
                {
                    try { overlay.Dispose(); } catch { }
                }
            }
        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void buttonMove_Click(object sender, EventArgs e)
        {

            buttonMove.Enabled = false;

            star_auto_check();

            buttonMove.Enabled = true;
        }

        private void timer_thingspeak_Tick(object sender, EventArgs e)
        {
            string s = string.Format("{0} {1}", 2, frame_id); // 2:FishEye2
            //コマンドライン引数に「"C:\test\1.txt"」を指定してメモ帳を起動する
         //   System.Diagnostics.Process.Start(@"""C:\tool\bin\thingspeak_send_frame_id_cs.exe""", s);
        }

        private void numericUpDownStarMin_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numericUpDownRoll_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // numericUpDownRoll stores degrees; update fish2 model roll and preserve current tiltX/tiltY
                double roll = (double)numericUpDownRoll.Value;
                // Read tilt values defensively to avoid exceptions if controls are not yet initialized
                double tiltX = 0.0, tiltY = 0.0;
                try { tiltX = (double)numericUpDownTiltX.Value; } catch { }
                try { tiltY = (double)numericUpDownTiltY.Value; } catch { }
                fish2.SetPointingError(tiltX, tiltY, roll);
                // If you need to refresh overlays immediately, force recalculation
                lastStarCalc = DateTime.MinValue; // force recalculation on next display update
            }
            catch
            {
                // ignore
            }
        }

        private void numericUpDownTiltX_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // numericUpDownRoll stores degrees; update fish2 model roll
                double roll = (double)numericUpDownRoll.Value;
                double tiltX = (double)numericUpDownTiltX.Value;
                double tiltY = (double)numericUpDownTiltY.Value;
                fish2.SetPointingError(tiltX, tiltY, roll);
                // If you need to refresh overlays immediately, force recalculation
                lastStarCalc = DateTime.MinValue; // force recalculation on next display update
            }
            catch { }
        }

        private void numericUpDownTiltY_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                // numericUpDownRoll stores degrees; update fish2 model roll
                double roll = (double)numericUpDownRoll.Value;
                double tiltX = (double)numericUpDownTiltX.Value;
                double tiltY = (double)numericUpDownTiltY.Value;
                fish2.SetPointingError(tiltX, tiltY, roll);
                // If you need to refresh overlays immediately, force recalculation
                lastStarCalc = DateTime.MinValue; // force recalculation on next display update
            }
            catch { }
        }
        private void buttonSavePointing_Click(object sender, EventArgs e)
        {
            try
            {
                // Read current values from controls and save to appSettings
                appSettings.Roll = (double)numericUpDownRoll.Value;
                appSettings.TiltX = (double)numericUpDownTiltX.Value;
                appSettings.TiltY = (double)numericUpDownTiltY.Value;
                // Save settings to file
                SettingsSave(appSettings);
                MessageBox.Show("Pointing values saved to settings.", "Save Pointing", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save pointing values: " + ex.Message, "Save Pointing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonSaveOverlay_Click(object sender, EventArgs e)
        {
            // overlayBitmap をロックして保存
            Bitmap copy = null;
            lock (overlayLock)
            {
                if (overlayBitmap != null)
                {
                    try { copy = (Bitmap)overlayBitmap.Clone(); } catch { copy = null; }
                }
            }

            if (copy == null)
            {
                MessageBox.Show("Overlay image is not available.", "Save Overlay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlays");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string fn = Path.Combine(dir, "overlay_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                copy.Save(fn, ImageFormat.Png);
                MessageBox.Show("Saved: " + fn, "Save Overlay", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save overlay: " + ex.Message, "Save Overlay", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { copy.Dispose(); } catch { }
            }
        }

        private void checkBoxDispAvg_CheckedChanged(object sender, EventArgs e)
        {

        }
          
        private void ButtonSave_Click_1(object sender, EventArgs e)
        {

        }

        private void ShowButton_Click_1(object sender, EventArgs e)
        {

        }

        private void CloseButton_Click_1(object sender, EventArgs e)
        {

        }

        private void ButtonSaveEnd_Click_1(object sender, EventArgs e)
        {

        }

        private void button_test_Click(object sender, EventArgs e)
        {
            string fn_t = @"D:\img_data\data\20260211_1s_1.png";
            Mat mat1 = Cv2.ImRead(fn_t, ImreadModes.Grayscale);
            Cv2.ImShow("mat1:", mat1);

            var starMaskConfig = new StarMaskConfig
            {
                /// <summary>恒星検出の動的しきい値 = 背景の平均 + ThresholdK * 標準偏差</summary>
                ThresholdK = 5.0,

                /// <summary>動的しきい値の下限（8bit階調換算の輝度）</summary>
                MinThreshold = 15.0,

                /// <summary>恒星とみなす最小面積 [px^2]（背景画像の解像度基準）。ノイズ除去用</summary>
                MinStarAreaPx = 1,

                /// <summary>恒星とみなす最大面積 [px^2]。これを超える明部は雲・光害などとして除外対象にしない</summary>
                MaxStarAreaPx = 60,

                /// <summary>各恒星の中心から除外する半径 [px]（瞬きによる輝度変化・測光中心の揺れ・
                /// 背景の実効時定数内でのわずかな日周運動ズレを吸収するための余裕）</summary>
                ExclusionRadiusPx = 4,

                /// <summary>1回のマスク生成で処理する恒星数の上限（処理時間の安全弁。天の川方向などで極端に
                /// 星数が多い場合に備える）</summary>
                MaxStars = 3000

            };
            var cloudMaskConfig = new CloudMaskConfig
            {
                /// <summary>恒星除去用オープニング(収縮→膨張)のカーネルサイズ [px]。
                /// 恒星の見かけの大きさ(PSF)より大きく、想定する最小の雲パッチより小さく設定する。
                /// 0にするとオープニングを行わない（恒星も含めた全ての明部を雲候補扱いにする）。</summary>
                OpeningSizePx = 15,

                /// <summary>雲候補抽出の動的しきい値 = (オープニング後画像の)平均 + ThresholdK * 標準偏差</summary>
                ThresholdK = 3.0,

                /// <summary>動的しきい値の下限（8bit階調換算の輝度）</summary>
                MinThreshold = 10.0,

                /// <summary>断片化した雲領域を連結するクロージング(膨張→収縮)のカーネルサイズ [px]。
                /// 0にするとクロージングを行わない。</summary>
                ClosingSizePx = 25,

                /// <summary>最終マスクに持たせる安全マージン [px]。雲の輪郭や月のハロー(滲み)は
                /// 2値化の境界より実際には広がっていることが多いため、外側に余裕を持たせて除外する。</summary>
                MarginPx = 15,

                /// <summary>これ未満の面積 [px^2] の孤立領域は雲とみなさず除外しない（誤判定抑制）。
                /// オープニング・クロージング後に残る小さなノイズ的領域を想定し、恒星除外に使う
                /// 面積よりも十分大きい値を設定する。</summary>
                MinCloudAreaPx = 50
            };


    sw.Reset();sw.Start();
            var starmask = MeteorDetection.StarMaskGenerator.GenerateValidMask(mat1, starMaskConfig);// 42ms
            var cloudmask = MeteorDetection.CloudMaskGenerator.GenerateValidMask(mat1, cloudMaskConfig);// 42ms
            long microseconds = sw.ElapsedTicks / (System.Diagnostics.Stopwatch.Frequency / (1000L * 1000L));
            label_mask.Text = "starmask: " + microseconds.ToString() + "[usec]";

            Cv2.ImShow("starmask:", starmask);
            starmask.ImWrite(@"D:\img_data\data\20260211_1s_1_starmask.png");

            Cv2.ImShow("cloudmask:", cloudmask);
            cloudmask.ImWrite(@"D:\img_data\data\20260211_1s_1_cloudmask.png");
            Cv2.WaitKey();

            var fish2 = new AllSkyCamera.FisheyeCameraModel(2608, 2608, 2.67 / 0.00345);
            double rolldeg = 180.0;// 180.0:  Roll angle in degrees 北が下の画像（現在のFish2の向き
            double tx =  0.8;
            double ty = -0.2;
            fish2.SetPointingError(tx, ty, rolldeg); // Set pointing error to zero for testing
            double az  = 266.9747;
            double alt = 38.389666;

            fish2.HorizontalToPixel(az, alt, out double px, out double py);
            Console.WriteLine("az,alt:({0}, {1}) px,py;({2}, {3})", az, alt, px, py);

            az = 0; alt = 35.01986;
            fish2.HorizontalToPixel(az, alt, out px, out py);
            Console.WriteLine("az,alt:({0}, {1}) px,py;({2}, {3})", az, alt, px, py);


            //string videoSourcee = @"D:\img_data\data\20260630_235211_566_1.avi";
            string videoSourcee = @"D:\img_data\data\output.mp4";
            string trackerCamHost = @"192.168.1.221";
            int cameraport = 22222;
            string outbasedir = @"D:\img_data\data\";

            //VideoCapture vc = new VideoCapture(videoSourcee);
            //using (var mat = new Mat())
            //{
            //    while (!vc.Read(mat))
            //    {                    
            //        Cv2.ImShow("GrabLoop", mat);
            //        if (Cv2.WaitKey(33) == 27) break; // ESC to exit
            //    }
            //}

            MeteorDetection.UsageExample.Run(videoSourcee, trackerCamHost, cameraport, outbasedir, fish2);

            return;

            SimpleBlobDetector.Params param = new SimpleBlobDetector.Params();
            param.MaxArea = 100000;
           // SimpleBlobDetector detector = SimpleBlobDetector.Create(param);

            // Detect blobs.

            //string fn = @"C: \Users\root\Pictures\Screenpresso\2021 - 03 - 07_12h15_31.png";
            string fn = @"C:\Users\root\Downloads\blob_test.jpeg";
            using (Mat img1 = new Mat(fn))
            {
                // detecting keypoints
                // FastFeatureDetector, StarDetector, SIFT, SURF, ORB, BRISK, MSER, GFTTDetector, DenseFeatureDetector, SimpleBlobDetector
                // SURF = Speeded Up Robust Features
                //var detector = SURF.Create(hessianThreshold: 400); //A good default value could be from 300 to 500, depending from the image contrast.
                var detector = SimpleBlobDetector.Create(param);
                var keypoints1 = detector.Detect(img1);
                
                KeyPoint maxkey = new KeyPoint( new Point2f(0,0),-1);
                foreach (var keyPoint in keypoints1)
                {
                    if (maxkey.Size < keyPoint.Size) maxkey = keyPoint;
                    Console.WriteLine("X: {0}, Y: {1}", keyPoint.Pt.X, keyPoint.Pt.Y);
                }
                Console.WriteLine("MAX X: {0}, Y: {1} Size: {2}", maxkey.Pt.X, maxkey.Pt.Y, maxkey.Size);


                Cv2.DrawKeypoints(img1, keypoints1, img1,null,DrawMatchesFlags.DrawRichKeypoints);
                Cv2.ImShow("mat2", img1);
                System.Threading.Thread.Sleep(3000);
            }

         
        }

        private void checkBox_WideDR_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_DispMode.Checked)
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            }
            else
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            }
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            int x = ((e.Location.X -204) * 2608 / 901);
            int y = ((e.Location.Y -  1) * 2608 / 901);

            try
            {
                richTextBox1.AppendText( " "+x.ToString()+","+y.ToString()+", 20, 0\n" );
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }
        private void checkBoxLoupe_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = false;
            try { enabled = checkBoxLoupe.Checked; } catch { }
            pictureBoxLoupe.Visible = enabled;
            if (enabled)
            {
                try { pictureBoxLoupe.BringToFront(); } catch { }
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // If loupe not enabled or no image, hide loupe
                bool loupeOn = false;
                try { loupeOn = checkBoxLoupe.Checked; } catch { loupeOn = false; }
                if (!loupeOn || pictureBox1.Image == null)
                {
                    pictureBoxLoupe.Visible = false;
                    return;
                }

                // Always keep loupe visible while enabled
                pictureBoxLoupe.Visible = true;

                // Decide whether we're actively moving the loupe (Shift down) or keeping it fixed
                bool shiftDown = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                // If Shift is down, update loupe screen position to follow the cursor and set loupeCenter.
                if (shiftDown)
                {
                    loupeCenter = new System.Drawing.Point(e.X, e.Y);
                    loupeCenterInitialized = true;
                try
                {
                    var ptScreen = pictureBox1.PointToScreen(new System.Drawing.Point(e.X, e.Y));
                    // Convert screen point to the loupe's parent client coordinates (handles when loupe is inside a panel)
                    System.Drawing.Point ptParentClient;
                    System.Windows.Forms.Control parent = null;
                    try { parent = pictureBoxLoupe?.Parent; } catch { parent = null; }
                    if (parent != null)
                    {
                        ptParentClient = parent.PointToClient(ptScreen);
                    }
                    else
                    {
                        ptParentClient = this.PointToClient(ptScreen);
                    }

                    int lx = ptParentClient.X + 20;
                    int ly = ptParentClient.Y + 20;
                    // keep inside parent bounds
                    System.Drawing.Size parentSize = (parent != null) ? parent.ClientSize : this.ClientSize;
                    lx = Math.Min(Math.Max(0, lx), parentSize.Width - pictureBoxLoupe.Width);
                    ly = Math.Min(Math.Max(0, ly), parentSize.Height - pictureBoxLoupe.Height);
                    pictureBoxLoupe.Location = new System.Drawing.Point(lx, ly);
                    try { pictureBoxLoupe.BringToFront(); } catch { }
                }
                catch { }
                }
                else
                {
                    // Shift is OFF: keep loupe position fixed. Initialize loupeCenter on first use.
                    if (!loupeCenterInitialized)
                    {
                        loupeCenter = new System.Drawing.Point(e.X, e.Y);
                        loupeCenterInitialized = true;
                    }
                    // Do not change pictureBoxLoupe.Location here (keep fixed on screen).
                }

                // Use the target point (in PictureBox client coords) for cropping/coordinate conversion.
                System.Drawing.Point targetPbPoint = shiftDown ? new System.Drawing.Point(e.X, e.Y) : loupeCenter;
                // Update loupe from current image
                try { UpdateLoupe(targetPbPoint); } catch { }
            }
            catch
            {
                // ignore any loupe errors
            }
        }

        // Update loupe display using a target point in pictureBox1 client coordinates.
        private void UpdateLoupe(System.Drawing.Point targetPbPoint)
        {
            // If loupe not enabled or no image, skip
            bool loupeOn = false;
            try { loupeOn = checkBoxLoupe.Checked; } catch { loupeOn = false; }
            if (!loupeOn) return;
            Image img = pictureBox1.Image;
            if (img == null) return;

            Rectangle imgRect = GetPictureBoxImageRect(pictureBox1);
            if (imgRect.IsEmpty) return;

            double scaleX = (double)img.Width / imgRect.Width;
            double scaleY = (double)img.Height / imgRect.Height;
            int ix = (int)((targetPbPoint.X - imgRect.X) * scaleX);
            int iy = (int)((targetPbPoint.Y - imgRect.Y) * scaleY);

            int sw = Math.Max(1, pictureBoxLoupe.Width);
            int sh = Math.Max(1, pictureBoxLoupe.Height);
            int srcW = Math.Max(1, (int)((sw / Math.Max(0.0001, loupeZoom)) * scaleX));
            int srcH = Math.Max(1, (int)((sh / Math.Max(0.0001, loupeZoom)) * scaleY));
            int sx = ix - srcW / 2;
            int sy = iy - srcH / 2;
            if (sx < 0) sx = 0;
            if (sy < 0) sy = 0;
            if (sx + srcW > img.Width) sx = Math.Max(0, img.Width - srcW);
            if (sy + srcH > img.Height) sy = Math.Max(0, img.Height - srcH);

            try
            {
                using (Bitmap src = new Bitmap(img))
                {
                    // Pre-compose overlay onto the source image so loupe shows stars
                    Bitmap ov = null;
                    lock (overlayLock)
                    {
                        if (overlayBitmap != null)
                        {
                            try { ov = (Bitmap)overlayBitmap.Clone(); } catch { ov = null; }
                        }
                    }
                    if (ov != null)
                    {
                        try
                        {
                            using (Graphics go = Graphics.FromImage(src))
                            {
                                go.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                                go.DrawImage(ov, new Rectangle(0, 0, src.Width, src.Height));
                            }
                        }
                        catch { }
                        try { ov.Dispose(); } catch { }
                    }

                    using (Bitmap crop = new Bitmap(srcW, srcH))
                    {
                        using (Graphics g = Graphics.FromImage(crop))
                        {
                            g.DrawImage(src, new Rectangle(0, 0, srcW, srcH), new Rectangle(sx, sy, srcW, srcH), GraphicsUnit.Pixel);
                        }
                        Bitmap display = new Bitmap(sw, sh);
                        using (Graphics g2 = Graphics.FromImage(display))
                        {
                            g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            g2.DrawImage(crop, new Rectangle(0, 0, sw, sh), new Rectangle(0, 0, srcW, srcH), GraphicsUnit.Pixel);
                        }
                        var old = pictureBoxLoupe.Image;
                        pictureBoxLoupe.Image = display;
                        try { old?.Dispose(); } catch { }
                        if ((DateTime.Now - lastLoupeLog).TotalMilliseconds > 500)
                        {
                            lastLoupeLog = DateTime.Now;
                        }
                    }
                }
            }
            catch { }
        }

        private void buttonUserSetLoad_Click(object sender, EventArgs e)
        {
            // PgUserSetLoad(nodeMap_iel); // camera接続後でなれけば上手くいかない。
        }

        private void checkBox_ExposureAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_ExposureAuto.Checked)
                PgExposureAuto(nodeMap_iel, true);
            else
            {
                PgExposureAuto(nodeMap_iel, false);
                PgSetExposure(nodeMap_iel, appSettings.Exposure * 1000);// expo[ms] in[usec]
            }
        }

        private void checkBox_GainAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_GainAuto.Checked)
                PgGainAuto(nodeMap_iel, true);
            else
            {
                PgGainAuto(nodeMap_iel, false);
                PgSetGain(nodeMap_iel, appSettings.Gain);
            }
        }

        private void numericUpDownExposureManual_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                double val = (double)numericUpDownExposureManual.Value; // ms
                appSettings.Exposure = val;
                // if exposure auto is off, apply immediately
                if (!checkBox_ExposureAuto.Checked)
                {
                    PgSetExposure(nodeMap_iel, appSettings.Exposure * 1000); // ms -> us
                }
            }
            catch { }
        }

        private void numericUpDownGain_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                double val = (double)numericUpDownGain.Value; // dB or unit used by camera
                appSettings.Gain = val;
                if (!checkBox_GainAuto.Checked)
                {
                    PgSetGain(nodeMap_iel, appSettings.Gain);
                }
            }
            catch { }
        }

        private void numericUpDownFramerate_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                double val = (double)numericUpDownFramerate.Value; // fps
                appSettings.Framerate = val;
                reqFramerate = val;
                // apply to PointGrey camera if used
                if (cam_maker == Camera_Maker.PointGreyCamera && nodeMap_iel != null)
                {
                    try
                    {
                        IFloat iRate = nodeMap_iel.GetNode<IFloat>("AcquisitionFrameRate");
                        if (iRate != null && iRate.IsWritable)
                        {
                            double setval = val;
                            if (iRate.Max < setval) setval = iRate.Max;
                            if (iRate.Min > setval) setval = iRate.Min;
                            iRate.Value = setval;
                        }
                    }
                    catch { }
                }
                // other camera types may read appSettings.Framerate elsewhere
            }
            catch { }
        }

        private void numericUpDownTiltX_ValueChanged_1(object sender, EventArgs e)
        {

        }

        private void labelRoll_Click(object sender, EventArgs e)
        {

        }

        private void pictureBoxLoupe_Click(object sender, EventArgs e)
        {

        }

        private void numericUpDownLoupeZoom_VisibleChanged(object sender, EventArgs e)
        {

        }

        private void labelDetMinTh_Click(object sender, EventArgs e)
        {

        }
    }
}

