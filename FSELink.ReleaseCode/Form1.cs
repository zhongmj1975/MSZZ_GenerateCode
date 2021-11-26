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

namespace FSELink.ReleaseCode
{
    public partial class Form1 : Form
    {
        bool blStart = false;
        public Form1()
        {
            InitializeComponent();
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

        private void button1_Click(object sender, EventArgs e)
        {
            ConfigurationHelper.GetConfig(AppDomain.CurrentDomain.BaseDirectory + "\\System.config");
            blStart = true;
            new Task(ExportFile).Start();
            new Task(CreateCode).Start();
            new Task(SendCodeToMSZZ).Start();
            LogHelper.WriteLog("码上增值数据发布、导出服务已启动！");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            blStart = false;
        }
    }
}
