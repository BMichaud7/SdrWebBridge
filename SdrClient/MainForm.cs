using System.Text.Json;
using System.Text.Json.Nodes;

namespace SdrClient;

public sealed class MainForm : Form
{
    // ── connection controls ───────────────────────────────────────
    private readonly TextBox   _tbHost     = new() { Text = "localhost", Width = 120 };
    private readonly TextBox   _tbPort     = new() { Text = "5672",      Width = 55  };
    private readonly TextBox   _tbUser     = new() { Text = "sdr_ctrl",  Width = 90  };
    private readonly TextBox   _tbPass     = new() { Text = "sdr_hw_test", Width = 100, UseSystemPasswordChar = true };
    private readonly Button    _btnConnect = new() { Text = "Connect",   Width = 80  };
    private readonly Label     _lblStatus  = new() { Text = "● Disconnected", ForeColor = Color.Tomato, AutoSize = true };

    // ── task controls ─────────────────────────────────────────────
    private readonly ComboBox  _cbType     = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox   _tbFreq     = new() { Text = "476.5",  Width = 80 };
    private readonly TextBox   _tbBw       = new() { Text = "20",     Width = 60 };
    private readonly TextBox   _tbSr       = new() { Text = "20",     Width = 60 };
    private readonly TextBox   _tbDur      = new() { Text = "2000",   Width = 70 };
    private readonly TextBox   _tbDestIp   = new() { Text = "127.0.0.1", Width = 100 };
    private readonly TextBox   _tbRank     = new() { Text = "2",      Width = 40 };
    private readonly Button    _btnSend    = new() { Text = "Send Task", Width = 90, Enabled = false };

    // ── stop controls ────────────────────────────────────────────
    private readonly TextBox   _tbTaskId   = new() { Width = 280, PlaceholderText = "task_id from response" };
    private readonly Button    _btnStop    = new() { Text = "Stop Task", Width = 90, Enabled = false };

    // ── response log ─────────────────────────────────────────────
    private readonly RichTextBox _rtbLog   = new() { ReadOnly = true, BackColor = Color.FromArgb(14,17,23), ForeColor = Color.FromArgb(205,217,229), Font = new Font("Consolas", 8.5f), Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
    private readonly Button    _btnClear   = new() { Text = "Clear", Width = 60, Anchor = AnchorStyles.Top | AnchorStyles.Right };

    private readonly AmqpSession _amqp = new();

    public MainForm()
    {
        Text            = "SDR Client — OpenRFStack";
        Size            = new Size(820, 560);
        MinimumSize     = new Size(700, 480);
        BackColor       = Color.FromArgb(9, 12, 16);
        ForeColor       = Color.FromArgb(205, 217, 229);
        Font            = new Font("Segoe UI", 9f);

        _cbType.Items.AddRange(["WIDEBAND", "NARROWBAND", "SNAPSHOT", "TRIGGERED", "DF"]);
        _cbType.SelectedIndex = 0;

        BuildLayout();

        _btnConnect.Click += OnConnect;
        _btnSend.Click    += OnSendTask;
        _btnStop.Click    += OnStopTask;
        _btnClear.Click   += (_, _) => _rtbLog.Clear();

        _amqp.StatusChanged  += s => Invoke(() => UpdateStatus(s));
        _amqp.MessageReceived += m => Invoke(() => LogResponse(m));

        FormClosing += (_, _) => _amqp.Dispose();
    }

    // ── layout ───────────────────────────────────────────────────

    private void BuildLayout()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.BackColor = Color.FromArgb(9, 12, 16);

        outer.Controls.Add(ConnPanel(), 0, 0);
        outer.Controls.Add(TaskPanel(), 0, 1);
        outer.Controls.Add(StopPanel(), 0, 2);
        outer.Controls.Add(LogPanel(),  0, 3);

        Controls.Add(outer);
    }

    private Panel SectionPanel(string title)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6) };
        var lbl = new Label
        {
            Text = title, AutoSize = false, Height = 20, Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(118, 131, 144),
            Padding = new Padding(0, 4, 0, 2)
        };
        p.Controls.Add(lbl);
        return p;
    }

    private Control ConnPanel()
    {
        var p   = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 8), WrapContents = false, BackColor = Color.Transparent };
        StyleCtrl(_tbHost); StyleCtrl(_tbPort); StyleCtrl(_tbUser); StyleCtrl(_tbPass);
        StyleBtn(_btnConnect);

        p.Controls.AddRange([
            Label("Host"), _tbHost, Label("Port"), _tbPort,
            Label("User"), _tbUser, Label("Pass"), _tbPass,
            _btnConnect, _lblStatus
        ]);
        return p;
    }

    private Control TaskPanel()
    {
        var p = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8), WrapContents = false, BackColor = Color.Transparent };
        StyleCtrl(_cbType); StyleCtrl(_tbFreq); StyleCtrl(_tbBw);
        StyleCtrl(_tbSr);   StyleCtrl(_tbDur);  StyleCtrl(_tbDestIp); StyleCtrl(_tbRank);
        StyleBtn(_btnSend);

        p.Controls.AddRange([
            Label("Type"), _cbType,
            Label("Freq (MHz)"), _tbFreq,
            Label("BW (MHz)"), _tbBw,
            Label("SR (MSPS)"), _tbSr,
            Label("Duration (ms)"), _tbDur,
            Label("Dest IP"), _tbDestIp,
            Label("Rank"), _tbRank,
            _btnSend
        ]);
        return p;
    }

    private Control StopPanel()
    {
        var p = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8), WrapContents = false, BackColor = Color.Transparent };
        StyleCtrl(_tbTaskId);
        StyleBtn(_btnStop);
        p.Controls.AddRange([Label("Task ID"), _tbTaskId, _btnStop]);
        return p;
    }

    private Control LogPanel()
    {
        var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 17, 23), Padding = new Padding(1) };
        _btnClear.FlatStyle = FlatStyle.Flat;
        _btnClear.FlatAppearance.BorderColor = Color.FromArgb(39, 45, 53);
        _btnClear.BackColor = Color.FromArgb(22, 27, 34);
        _btnClear.ForeColor = Color.FromArgb(118, 131, 144);
        _btnClear.Dock = DockStyle.Bottom;

        wrapper.Controls.Add(_rtbLog);
        wrapper.Controls.Add(LogHeader());
        wrapper.Controls.Add(_btnClear);
        return wrapper;
    }

    private Control LogHeader()
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.FromArgb(22, 27, 34) };
        p.Controls.Add(new Label { Text = "Responses", AutoSize = true, Location = new Point(6, 4), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = Color.FromArgb(118, 131, 144) });
        return p;
    }

    // ── helpers ──────────────────────────────────────────────────

    private static Label Label(string text) => new()
    {
        Text = text, AutoSize = true,
        ForeColor = Color.FromArgb(118, 131, 144),
        Margin = new Padding(6, 5, 2, 3),
        Font = new Font("Segoe UI", 8.5f)
    };

    private static void StyleCtrl(Control c)
    {
        c.BackColor = Color.FromArgb(22, 27, 34);
        c.ForeColor = Color.FromArgb(205, 217, 229);
        c.Margin    = new Padding(0, 3, 4, 3);
        if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleBtn(Button b)
    {
        b.BackColor = Color.FromArgb(31, 111, 235);
        b.ForeColor = Color.White;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.Margin     = new Padding(4, 2, 0, 2);
        b.Height     = 24;
        b.Font       = new Font("Segoe UI", 8.5f, FontStyle.Bold);
    }

    // ── event handlers ───────────────────────────────────────────

    private void OnConnect(object? s, EventArgs e)
    {
        if (_amqp.IsConnected)
        {
            _amqp.Disconnect();
            _btnConnect.Text = "Connect";
            _btnSend.Enabled = _btnStop.Enabled = false;
            return;
        }
        try
        {
            _amqp.Connect(_tbHost.Text.Trim(), int.Parse(_tbPort.Text.Trim()),
                          _tbUser.Text.Trim(), _tbPass.Text);
            _btnConnect.Text = "Disconnect";
            _btnSend.Enabled = _btnStop.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSendTask(object? s, EventArgs e)
    {
        try
        {
            var taskType = _cbType.SelectedItem?.ToString() ?? "WIDEBAND";
            var freqHz   = double.Parse(_tbFreq.Text) * 1e6;
            var bwHz     = double.Parse(_tbBw.Text)   * 1e6;
            var srSps    = double.Parse(_tbSr.Text)   * 1e6;
            var durMs    = int.Parse(_tbDur.Text);
            var destIp   = _tbDestIp.Text.Trim();
            var rank     = int.Parse(_tbRank.Text);
            var reqId    = Guid.NewGuid().ToString();

            var req = new JsonObject
            {
                ["msg_type"]        = "TASK_REQUEST",
                ["schema_version"]  = "2.0",
                ["request_id"]      = reqId,
                ["timestamp_ms"]    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["task_type"]       = taskType,
                ["rank"]            = rank,
                ["schedule"]        = new JsonObject { ["mode"] = "IMMEDIATE", ["duration_ms"] = durMs },
                ["rf"]              = new JsonObject
                {
                    ["center_freq_hz"]  = freqHz,
                    ["bandwidth_hz"]    = bwHz,
                    ["sample_rate_sps"] = srSps,
                    ["rx_count"]        = 1
                },
                ["streaming"]       = new JsonObject { ["dest_ip"] = destIp }
            };

            if (taskType == "WIDEBAND")
                req["wideband"] = new JsonObject { ["record_raw_iq"] = true, ["fft_size"] = 2048 };

            var json = req.ToJsonString();
            _amqp.Send(json);
            Log($"SENT  [{taskType}]  req_id={reqId[..8]}  {freqHz/1e6:F3} MHz  BW={bwHz/1e6:F0} MHz  dur={durMs} ms", Color.FromArgb(79, 158, 255));
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}", Color.Tomato);
        }
    }

    private void OnStopTask(object? s, EventArgs e)
    {
        var taskId = _tbTaskId.Text.Trim();
        if (string.IsNullOrEmpty(taskId)) return;
        try
        {
            var req = new JsonObject
            {
                ["msg_type"]     = "TASK_STOP",
                ["request_id"]   = Guid.NewGuid().ToString(),
                ["task_id"]      = taskId,
                ["timestamp_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["reason"]       = "user request"
            };
            _amqp.Send(req.ToJsonString());
            Log($"SENT  [TASK_STOP]  task_id={taskId}", Color.FromArgb(210, 153, 34));
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}", Color.Tomato);
        }
    }

    private void UpdateStatus(string status)
    {
        _lblStatus.Text      = $"● {status}";
        _lblStatus.ForeColor = status == "Connected" ? Color.FromArgb(63, 185, 80) : Color.Tomato;
    }

    private void LogResponse(string json)
    {
        try
        {
            var doc    = JsonDocument.Parse(json);
            var root   = doc.RootElement;
            var status = root.TryGetProperty("status", out var sv) ? sv.GetString() : null;
            var taskId = root.TryGetProperty("task_id", out var tv) ? tv.GetString() : null;
            var reason = root.TryGetProperty("reject_reason", out var rv) ? rv.GetString() : null;
            var port   = root.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0
                           ? streams[0].TryGetProperty("udp_port", out var pv) ? pv.GetInt32() : 0
                           : 0;

            var color = status switch
            {
                "ACCEPTED" => Color.FromArgb(63, 185, 80),
                "REJECTED" => Color.Tomato,
                _          => Color.FromArgb(205, 217, 229)
            };

            var text = $"RECV  [{status}]";
            if (taskId != null) text += $"  task_id={taskId[..Math.Min(8, taskId.Length)]}";
            if (port > 0)       text += $"  udp_port={port}";
            if (reason != null) text += $"  reason={reason}";

            if (taskId != null && status == "ACCEPTED")
                Invoke(() => _tbTaskId.Text = taskId);

            Log(text, color);
        }
        catch
        {
            Log($"RECV  {json}", Color.FromArgb(118, 131, 144));
        }
    }

    private void Log(string text, Color color)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        _rtbLog.SelectionStart  = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;
        _rtbLog.SelectionColor  = Color.FromArgb(118, 131, 144);
        _rtbLog.AppendText($"[{ts}] ");
        _rtbLog.SelectionStart  = _rtbLog.TextLength;
        _rtbLog.SelectionColor  = color;
        _rtbLog.AppendText(text + Environment.NewLine);
        _rtbLog.ScrollToCaret();
    }
}
