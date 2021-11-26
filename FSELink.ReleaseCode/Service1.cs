using FSELink.SupperCode;
using FSELink.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using FSELink.DAL;

namespace FSELink.ReleaseCode
{
    public partial class MSZZ_ReleaseCode : ServiceBase
    {
        bool blStart = false;
        public MSZZ_ReleaseCode()
        {
            
            InitializeComponent();            
            
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                ConfigurationHelper.GetConfig(AppDomain.CurrentDomain.BaseDirectory+"\\System.config");
                blStart = true;
                new Task(ExportFile).Start();
                new Task(CreateCode).Start();
                new Task(SendCodeToMSZZ).Start();
                LogHelper.WriteLog("码上增值数据发布、导出服务已启动！");
            }
            catch (Exception ex)
            {
                LogHelper.WriteException(ex);
                blStart = false;
                this.Stop();
            }
        }


        protected override void OnStop()
        {
            blStart = false;
            this.Stop();
            LogHelper.WriteLog("码上增值数据发布、导出服务已停止！");
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



    }
}
