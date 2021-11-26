using FSELink.Bussiness;
using FSELink.Entities;
using FSELink.SupperCode;
using FSELink.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class frmMain : Form
    {
        bool blStart = false;
        bool blInit = false;
        string configFile = "System.config";
        
        public frmMain()
        {
            try
            {
                InitializeComponent();
                ConfigurationHelper.GetConfig(configFile);
                checkBox1.Checked = SystemInfo.AutoStart;
                this.Text += "-V" + Application.ProductVersion;
                blInit = true;
            }
            catch(Exception ex)
            {
                LogHelper.WriteException(ex);
                blInit = false;
                toolStripStatusLabel1.Text = ex.Message;
            }
      
        }


 
        private void Form1_Load(object sender, EventArgs e)
        {
            if(SystemInfo.AutoStart)
            {
                button2_Click_1(sender,e);
            }
        }


        private void button2_Click_1(object sender, EventArgs e)
        {
            
            if(!blStart)
            {
                button2.Text = "停止服务";
                toolStripStatusLabel1.Text = "服务已启动";
                button2.BackColor = Color.Green;               
                statusStrip1.BackColor = Color.Green;
                blStart = true;
                new Task(ExportFile).Start();
                new Task(CreateCode).Start();
                new Task(SendCodeToMSZZ).Start();
            }
            else
            {
                button2.Text = "启动服务";
                toolStripStatusLabel1.Text = "服务已停止";
                button2.BackColor = Color.Red;
                statusStrip1.BackColor = Color.Red;
                blStart = false;
                
            }
        }

       
        private async void ExportFile()
        {
            SupperCodeHelper helper;
            while (true)
            {
                helper = new SupperCodeHelper();
                helper.IsServerStart = blStart;
                if (!blStart) return;
                Thread.Sleep(SystemInfo.ServiceInterval);
               await helper.ExportFileAsync();
            }
        }

        private async void CreateCode()
        {
            SupperCodeHelper helper;
            while (true)
            {
                helper = new SupperCodeHelper();
                helper.IsServerStart = blStart;
                if (!blStart) return;
                Thread.Sleep(SystemInfo.ServiceInterval);                
                await helper.GenerateCodeAsync();
            }
        }

        private async void SendCodeToMSZZ()
        {
            SupperCodeHelper helper;
            while (true)
            {
                helper = new SupperCodeHelper();
                helper.IsServerStart = blStart;
                if (!blStart) return;
                Thread.Sleep(SystemInfo.ServiceInterval);
                await helper.SendDataToMSZZ();
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(blStart)
            {
                if( MessageBox.Show("服务已启用，不能直接终止服务，是否需要退出生码系统？","提示信息",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)==DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                else
                    blStart = false;
            }
        }

        private void frmMain_MouseDown(object sender, MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Right)
            {
                contextMenuStrip1.Show(this, e.X, e.Y);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //string abd = checkBox1.Checked.ToString();
            ConfigHelper.SetValue(configFile, "AutoStart", checkBox1.Checked.ToString());
        }
    }
}
