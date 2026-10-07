// =====================================================================
// โปรแกรมควบคุมแขนกลบน Windows ผ่านสาย USB (ใช้แทนแอปมือถือชั่วคราว)
//
// ส่งคำสั่งรูปแบบเดียวกับแอปมือถือ "S<เลขเซอร์โว>:<องศา>\n" ไปที่ Arduino Nano
// ผ่านพอร์ต COM ของสาย USB (เช่น COM7 - USB-SERIAL CH340)
//
// คอมไพล์ด้วยคอมไพเลอร์ C# ที่มากับ Windows:
//   csc /target:winexe /codepage:65001 /r:System.Management.dll RobotArmController.cs
// =====================================================================

using System;
using System.Drawing;
using System.IO.Ports;
using System.Management;
using System.Windows.Forms;

class MainForm : Form
{
    // องศาเริ่มต้นตอนเปิดโปรแกรม และองศาที่ปุ่ม "กลับท่าเริ่มต้น" สั่งไป
    const int HomeAngle = 0;
    // มือจับ (เซอร์โว 4) ขยับได้จริงแค่ช่วงแคบ: 0° = หุบ, ~45° = เปิด, เกิน 50° จะดันชนตัวกั้น
    const int GripClosed = 0;
    const int GripOpen = 45;
    // องศาสูงสุดที่ข้อต่อแต่ละตัวขยับได้จริง (เกินนี้จะชนโครง): ฐานหมุน, ไหล่, ข้อศอก, มือจับ
    readonly int[] jointMax = { 180, 100, 100, 50 };

    readonly string[] jointNames = { "ฐานหมุน", "ไหล่", "ข้อศอก", "มือจับ" };
    readonly string[] jointPins = { "D3", "D5", "D6", "D9" };

    SerialPort port;
    ComboBox cboPorts;
    Button btnConnect;
    Label lblStatus;
    TrackBar[] bars = new TrackBar[4];
    Label[] lblAngles = new Label[4];
    TextBox txtLog, txtCommand;
    Timer sendTimer;

    int[] sentAngles = { -1, -1, -1, -1 };  // องศาล่าสุดที่ส่งไปแล้ว (-1 = ยังไม่ได้ส่ง)
    DateTime readyAt;                        // Nano รีเซ็ตตอนเปิดพอร์ต ต้องรอให้พร้อมก่อนส่ง
    bool nanoReady;                          // ได้รับข้อความ "Ready" ครั้งแรกหลังเชื่อมต่อแล้ว
    string rxTail = "";                      // ข้อความท้ายสุดที่ได้รับ ใช้ตรวจหาคำว่า "Ready"

    static readonly Font UiFont = new Font("Leelawadee UI", 10f);
    static readonly Font BoldFont = new Font("Leelawadee UI", 11f, FontStyle.Bold);

    public MainForm()
    {
        Text = "ควบคุมแขนกล (USB)";
        ClientSize = new Size(560, 700);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = UiFont;

        var title = new Label {
            Text = "ควบคุมแขนกลผ่านสาย USB", Font = new Font("Leelawadee UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 71, 161), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 10, 560, 36)
        };
        Controls.Add(title);

        // ---- เลือกพอร์ตและเชื่อมต่อ ----
        Controls.Add(new Label { Text = "พอร์ต:", Bounds = new Rectangle(20, 62, 50, 24) });
        cboPorts = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(72, 58, 250, 28) };
        Controls.Add(cboPorts);
        var btnRefresh = MakeButton("รีเฟรช", new Rectangle(330, 56, 80, 32), Color.FromArgb(120, 144, 156));
        btnRefresh.Click += (s, e) => LoadPorts();
        btnConnect = MakeButton("เชื่อมต่อ", new Rectangle(418, 56, 122, 32), Color.FromArgb(21, 101, 192));
        btnConnect.Click += (s, e) => ToggleConnection();

        lblStatus = new Label {
            Text = "สถานะ: ยังไม่เชื่อมต่อ", ForeColor = Color.FromArgb(198, 40, 40), AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 94, 560, 24)
        };
        Controls.Add(lblStatus);

        // ---- แถบเลื่อน 4 ข้อต่อ ----
        for (int i = 0; i < 4; i++)
        {
            int y = 126 + i * 78;
            lblAngles[i] = new Label { Font = BoldFont, Bounds = new Rectangle(20, y, 520, 26) };
            Controls.Add(lblAngles[i]);
            bars[i] = new TrackBar {
                Minimum = 0, Maximum = jointMax[i], Value = HomeAngle, TickFrequency = jointMax[i] / 10, SmallChange = 1, LargeChange = 10,
                Bounds = new Rectangle(14, y + 26, 532, 45), BackColor = BackColor
            };
            int index = i;
            bars[i].ValueChanged += (s, e) => UpdateAngleLabel(index);
            Controls.Add(bars[i]);
            UpdateAngleLabel(i);
        }

        // ---- ปุ่มควบคุม ----
        var btnOpen = MakeButton("เปิดมือจับ (" + GripOpen + "°)", new Rectangle(20, 444, 255, 38), Color.FromArgb(46, 125, 50));
        btnOpen.Click += (s, e) => bars[3].Value = GripOpen;
        var btnClose = MakeButton("หุบมือจับ (" + GripClosed + "°)", new Rectangle(285, 444, 255, 38), Color.FromArgb(239, 108, 0));
        btnClose.Click += (s, e) => bars[3].Value = GripClosed;
        var btnReset = MakeButton("กลับท่าเริ่มต้น (" + HomeAngle + "°)", new Rectangle(20, 490, 520, 38), Color.FromArgb(69, 90, 100));
        btnReset.Click += (s, e) => { foreach (var b in bars) b.Value = HomeAngle; };

        // ---- ส่งคำสั่งเอง (เช่น S1:120, SCAN, AT+VERSION) ----
        txtCommand = new TextBox { Bounds = new Rectangle(20, 540, 420, 28) };
        txtCommand.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SendManual(); e.SuppressKeyPress = true; } };
        Controls.Add(txtCommand);
        var btnSend = MakeButton("ส่ง", new Rectangle(448, 538, 92, 30), Color.FromArgb(2, 119, 189));
        btnSend.Click += (s, e) => SendManual();

        // ---- ข้อความตอบกลับจาก Nano ----
        txtLog = new TextBox {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f), BackColor = Color.White, Bounds = new Rectangle(20, 576, 520, 110)
        };
        Controls.Add(txtLog);

        // ส่งเฉพาะองศาที่เปลี่ยน ทุก 60 ms (กันส่งรัวจนบอร์ดรับไม่ทันตอนลากแถบ)
        sendTimer = new Timer { Interval = 60 };
        sendTimer.Tick += (s, e) => SendChangedAngles();
        sendTimer.Start();

        FormClosing += (s, e) => ClosePort();
        LoadPorts();
    }

    Button MakeButton(string text, Rectangle bounds, Color color)
    {
        var b = new Button {
            Text = text, Bounds = bounds, BackColor = color, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = BoldFont, Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        Controls.Add(b);
        return b;
    }

    void UpdateAngleLabel(int i)
    {
        lblAngles[i].Text = string.Format("เซอร์โว {0} · {1} ({2}):  {3}°     [0–{4}°]", i + 1, jointNames[i], jointPins[i], bars[i].Value, jointMax[i]);
    }

    // แสดงพอร์ต COM พร้อมชื่ออุปกรณ์ และเลือกพอร์ตของ Arduino (CH340) ให้อัตโนมัติ
    void LoadPorts()
    {
        cboPorts.Items.Clear();
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = (string)obj["Name"];
                    int start = name.LastIndexOf("(COM");
                    string com = name.Substring(start + 1).TrimEnd(')');
                    cboPorts.Items.Add(com + " - " + name.Substring(0, start).Trim());
                }
            }
        }
        catch
        {
            foreach (string com in SerialPort.GetPortNames()) cboPorts.Items.Add(com);
        }

        for (int i = 0; i < cboPorts.Items.Count; i++)
        {
            string item = cboPorts.Items[i].ToString();
            if (item.Contains("CH340") || item.Contains("Arduino") || item.Contains("USB-SERIAL")) { cboPorts.SelectedIndex = i; break; }
        }
        if (cboPorts.SelectedIndex < 0 && cboPorts.Items.Count > 0) cboPorts.SelectedIndex = 0;
    }

    void ToggleConnection()
    {
        if (port != null && port.IsOpen) { ClosePort(); return; }
        if (cboPorts.SelectedItem == null) { MessageBox.Show("ไม่พบพอร์ต COM กรุณาเสียบสาย USB ของ Nano แล้วกดรีเฟรช"); return; }

        string com = cboPorts.SelectedItem.ToString().Split(' ')[0];
        try
        {
            port = new SerialPort(com, 9600) { NewLine = "\n", DtrEnable = true };
            port.DataReceived += (s, e) => {
                string data;
                try { data = port.ReadExisting(); } catch { return; }
                BeginInvoke((Action)(() => AppendLog(data)));
            };
            port.Open();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เปิดพอร์ต " + com + " ไม่ได้\n\n" + ex.Message +
                "\n\nตรวจว่าปิด Serial Monitor ของ Arduino IDE แล้ว", "เชื่อมต่อไม่สำเร็จ");
            port = null;
            return;
        }

        // รอ Nano รีเซ็ตเสร็จ: เริ่มส่งเมื่อได้รับ "Ready" (หรือครบ 4 วินาที กรณีไม่ได้รับ)
        nanoReady = false;
        rxTail = "";
        readyAt = DateTime.Now.AddSeconds(4);
        for (int i = 0; i < 4; i++) sentAngles[i] = -1;  // บังคับส่งตำแหน่งแถบทั้งหมดหลังเชื่อมต่อ
        btnConnect.Text = "ตัดการเชื่อมต่อ";
        btnConnect.BackColor = Color.FromArgb(158, 158, 158);
        lblStatus.Text = "สถานะ: เชื่อมต่อแล้ว " + com;
        lblStatus.ForeColor = Color.FromArgb(46, 125, 50);
    }

    void ClosePort()
    {
        if (port != null) { try { port.Close(); } catch { } port = null; }
        btnConnect.Text = "เชื่อมต่อ";
        btnConnect.BackColor = Color.FromArgb(21, 101, 192);
        lblStatus.Text = "สถานะ: ยังไม่เชื่อมต่อ";
        lblStatus.ForeColor = Color.FromArgb(198, 40, 40);
    }

    void SendChangedAngles()
    {
        if (port == null || !port.IsOpen || DateTime.Now < readyAt) return;
        nanoReady = true;
        for (int i = 0; i < 4; i++)
        {
            if (bars[i].Value == sentAngles[i]) continue;
            if (!Write("S" + (i + 1) + ":" + bars[i].Value)) return;
            sentAngles[i] = bars[i].Value;
        }
    }

    void SendManual()
    {
        string cmd = txtCommand.Text.Trim();
        if (cmd.Length == 0) return;
        if (port == null || !port.IsOpen) { MessageBox.Show("กรุณากดเชื่อมต่อก่อน"); return; }
        if (Write(cmd)) txtCommand.Clear();
    }

    bool Write(string command)
    {
        try { port.Write(command + "\n"); return true; }
        catch (Exception ex)
        {
            ClosePort();
            MessageBox.Show("การเชื่อมต่อหลุด: " + ex.Message, "ข้อผิดพลาด");
            return false;
        }
    }

    void AppendLog(string data)
    {
        txtLog.AppendText(data.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        if (txtLog.TextLength > 20000) txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - 10000);

        // Nano พิมพ์ "Ready" ทุกครั้งที่เริ่มทำงาน
        rxTail += data;
        if (rxTail.Length > 200) rxTail = rxTail.Substring(rxTail.Length - 200);
        if (port == null || !port.IsOpen || !rxTail.Contains("Ready")) return;
        rxTail = "";

        if (!nanoReady)
        {
            // ครั้งแรกหลังเชื่อมต่อ: Nano พร้อมแล้ว เริ่มส่งตำแหน่งแถบได้เลย
            nanoReady = true;
            readyAt = DateTime.Now;
        }
        else
        {
            // Nano รีสตาร์ทเองระหว่างใช้งาน (มักเกิดจากไฟตกตอนเซอร์โวออกแรงมาก)
            // หลังรีสตาร์ท Nano สั่งเซอร์โวทุกตัวไปที่องศาเริ่มต้น ปรับแถบให้ตรงกับของจริง
            for (int i = 0; i < 4; i++)
            {
                sentAngles[i] = HomeAngle;
                bars[i].Value = HomeAngle;
            }
            lblStatus.Text = "สถานะ: Nano รีสตาร์ทเอง (ไฟเลี้ยงเซอร์โวไม่พอ?) · เซอร์โวกลับไปที่ " + HomeAngle + "°";
            lblStatus.ForeColor = Color.FromArgb(239, 108, 0);
        }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}
