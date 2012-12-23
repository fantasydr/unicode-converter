using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UniFileChecker
{
    public partial class MainForm : Form
    {
        string defaultTitle;
        public MainForm()
        {
            InitializeComponent();

            defaultTitle = this.Text;
            txtExt.Text = "*.ks;*.tjs;*.txt";
            txtPath.Text = Directory.GetCurrentDirectory();
        }

        private bool CheckUnicode(string file)
        {
            using (StreamReader sr = new StreamReader(file, Encoding.Default, true))
            {
                sr.ReadToEnd();
                return sr.CurrentEncoding.CodePage == Encoding.Unicode.CodePage;
            }
        }

        private void ConvertToUnicode(string file, bool backup)
        {
            string content;
            Encoding curEncode;
            using (StreamReader sr = new StreamReader(file, Encoding.Default, true))
            {
                content = sr.ReadToEnd();
                curEncode = sr.CurrentEncoding;
            }

            if (backup)
            {
                int count = 1;
                string newfile = file + ".bak";
                while(File.Exists(newfile))
                {
                    newfile = file + "." + count.ToString() + ".bak";
                    count++;
                }
                File.Copy(file, newfile);
            }

            byte[] original= curEncode.GetBytes(content);
            byte[] bytes = Encoding.Convert(curEncode, Encoding.Unicode, original);
            
            using (StreamWriter sw = new StreamWriter(file, false, Encoding.Unicode))
            {
                string target = Encoding.Unicode.GetString(bytes);
                sw.Write(target);
            }
        }

        private int ScanFiles(string path, string[] exts)
        {
            SearchOption op = chkAll.Checked? SearchOption.AllDirectories:SearchOption.TopDirectoryOnly;
            int count = 0;
            foreach (string extEntry in exts)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(path, extEntry, op);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("无法获取文件列表:\n" + ex.Message, defaultTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                }
                
                foreach(string file in files)
                {
                    count++;
                    if (count % 100 == 0)
                    {
                        this.BeginInvoke(new ThreadStart(delegate()
                        {
                            this.Text = defaultTitle + " - " + file;
                        }));
                    }

                    try
                    {
                        if (!CheckUnicode(file))
                        {
                            ascii.Add(file);
                        }
                    }
                    catch (Exception exp)
                    {
                        bad.Add(exp.Message + ":" + file);
                    }
                }
            }

            return count;
        }

        private void ConvertFiles(bool backup)
        {
            bad.Clear();

            foreach (string file in ascii)
            {
                try
                {
                    this.BeginInvoke(new ThreadStart(delegate()
                    {
                        this.Text = defaultTitle + " - " + file;
                    }));

                    ConvertToUnicode(file, backup);
                }
                catch (Exception exp)
                {
                    bad.Add(exp.Message + ":" + file);
                }
            }
        }

        List<string> ascii = new List<string>();
        List<string> bad = new List<string>();

        private void btnScan_Click(object sender, EventArgs e)
        {
            string ext = txtExt.Text;
            string path = txtPath.Text;

            if (!Directory.Exists(path))
            {
                MessageBox.Show("路径无效");
                return;
            }
            char[] splitter = new char[] { ';', ',', '|' };
            string[] exts = ext.Split(splitter);
            if (exts.Length == 0)
            {
                MessageBox.Show("无效的扩展名，请使用;,|作为分隔符，例如*.ks;*.txt");
                return;
            }

            ascii.Clear();
            bad.Clear();
            btnScan.Enabled = false;
            btnConvert.Enabled = false;

            // 开始扫描文件
            int count = 0;
            Thread t = new Thread(new ThreadStart (delegate(){
                count = ScanFiles(path, exts);
            }));
            t.Start();
            while (!t.Join(100))
            {
                Application.DoEvents();
            }

            btnScan.Enabled = true;
            this.Invoke(new ThreadStart(delegate()
            {
                this.Text = defaultTitle;
            }));
            btnConvert.Enabled = ascii.Count > 0;

            // 生成报告
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("已扫描 {0} 个文件, {1} 个非Unicode-LE文件, {2} 个错误文件。", count, ascii.Count, bad.Count);
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("点击“转Unicode”按钮可将扫描结果批量转为Unicode-LE。");
            sb.AppendLine("");
            if (ascii.Count > 0)
            {
                sb.AppendLine("================================");
                sb.AppendLine("非 Unicode-LE 文件:");
                sb.AppendLine("================================");
                foreach (string file in ascii)
                {
                    sb.AppendLine(file);
                }
            }

            if (bad.Count > 0)
            {
                sb.AppendLine("================================");
                sb.AppendLine("检查时出现错误的文件");
                sb.AppendLine("================================");
                foreach (string file in bad)
                {
                    sb.AppendLine(file);
                }
            }

            txtLog.Text = sb.ToString();
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            DialogResult ret = MessageBox.Show("是否备份原始文件？", "批量转Unicode-LE", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (ret == DialogResult.Cancel)
            {
                return;
            }
            bool backup = (ret == DialogResult.Yes);

            btnConvert.Enabled = false;

            // 开始转换文件
            Thread t = new Thread(new ThreadStart(delegate()
            {
                ConvertFiles(backup);
            }));
            t.Start();
            while (!t.Join(100))
            {
                Application.DoEvents();
            }
            this.Invoke(new ThreadStart(delegate()
            {
                this.Text = defaultTitle;
            }));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("");
            sb.AppendFormat("尝试转换 {0} 个文件，成功 {1} 个，失败 {2} 个。", ascii.Count, ascii.Count - bad.Count, bad.Count);
            sb.AppendLine("");
            if (bad.Count > 0)
            {
                sb.AppendLine("================================");
                sb.AppendLine("转换时出现错误的文件");
                sb.AppendLine("================================");
                foreach (string file in bad)
                {
                    sb.AppendLine(file);
                }
            }

            txtLog.Text += sb.ToString();
        }

        private void btnPathSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = txtPath.Text;
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtPath.Text = fbd.SelectedPath;
            }            
        }
    }
}
