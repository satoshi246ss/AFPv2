using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using OpenCvSharp.Blob;
//using VideoInputSharp;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.IO;
//using PylonC.NETSupportLibrary;
//using MtLibrary;
using MtLibrary2;

namespace AFPv2                                                                                                                                                                                                                                                                                                 
{
    public partial class Form1 : Form
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        Star star = new Star();

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

            IplImageInit();

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
            diskspace = cDrive.TotalFreeSpace;
            timerMTmonSend .Start();

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
            RichTextBox rtb = (RichTextBox)sender;　//objectをキャストする
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
            this.Close();
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
        }

        private void ObsStart_Click(object sender, EventArgs e)
        {
            richTextBox1.AppendText(DateTime.Now.ToString()+ ":Obs. started.");
            NLogInfo("Obs. started.");
            //PGR
            if (cam_maker == Camera_Maker.PointGreyCamera)
            {
                Task.Run(() => OpenPGRcamera());
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
            StreamReader sr = new StreamReader(@"../../mask_data.csv");
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
            diskspace = cDrive.TotalFreeSpace;
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
                        double scale = 8.0;
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

                // Mask 描画
                var imgtmp = Cv2.Split(img_dmk3);
                var imgtmp2 = ~img_mask; //反転
                
                Cv2.Add(imgtmp[2], imgtmp2 / 4, imgtmp[2]);
                Cv2.Merge(imgtmp, img_dmk3);
                imgtmp2.Dispose();
                imgtmp[0].Dispose();
                imgtmp[1].Dispose();
                imgtmp[2].Dispose();

                double k1 = 1.3333; //4deg 
                double k2 = 0.3333; //直径1deg
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
                    Cv2.Circle(img_dmk3, (OpenCvSharp.Point)Point1, 5, new Scalar(0, 255, 0));       // Arrow

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 90);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 90);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 180);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 180);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 270);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 270);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(230, 105, 0));

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
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(230, 105, 0));
                    //Cv.Line(img_dmk3, Point1, Point2, new Scalar(0, 205, 0));

                    Point1 = Rotation(OCPoint, k1 * roa, theta_c + 180);
                    Point2 = Rotation(OCPoint, k2 * roa, theta_c + 180);
                    Cv2.Line(img_dmk3, (OpenCvSharp.Point)Point1, (OpenCvSharp.Point)Point2, new Scalar(0, 205, 0));
                    Cv2.Circle(img_dmk3, (OpenCvSharp.Point)Point1, 5, new Scalar(0, 255, 0));       // Arrow

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

                // Star display for Fish2
                if (appSettings.NoCapDev == 1)
                {
                    int cx, cy, r_mag, i;
                    for (i = 0; i < star.Count; ++i)
                    {
                        get_star_disp_pos(i, 0, 0, appSettings.Theta, appSettings.FocalLength, appSettings.Ccdpx, appSettings.Ccdpx, out cx, out cy, out r_mag);
                        if (cx > -99990)
                        {
                            OCPoint.X = (int)(appSettings.Xoa + cx);
                            OCPoint.Y = (int)(appSettings.Yoa + cy);
                            Cv2.Circle(img_dmk3, OCPoint, 2 * r_mag, new Scalar(0, 255, 0));
                        }
                    }
                }

                try
                {
                    //Cv2.ImShow("PB test", img_dmk3);//Cv2.WaitKey();
                    Cv2.ImShow("img-avg", imgAvg.PyrDown().PyrDown());
                    //Cv2.ImShow("img-avg", img_dmk3.PyrDown().PyrDown());
                    pictureBox1.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(img_dmk3);

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
            Console.WriteLine("timer_dispO() : " + frame_id.ToString() + ": (" + sw.ElapsedMilliseconds.ToString() + ")ms " + microseconds);


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
            //String str = String.Format("ID:{0,6:D1} ", imgdata.id) + imgdata.t.ToString("yyyyMMdd_HHmmss_fff") + String.Format(" ({0,6:F1},{1,6:F1})({2,6:F1})", gx, gy, max_val);
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
            string s = string.Format("(x,y)=({0},{1})\n", e.X, e.Y);
            this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, s });
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
    }
}

