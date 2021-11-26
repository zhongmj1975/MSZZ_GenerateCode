﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace FSELink.Utilities
{
    /// <summary>
    /// 日志帮助类
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        /// 写入日志到本地TXT文件
        /// 注：日志文件名为"A_log.txt",目录为根目录
        /// </summary>
        /// <param name="log">日志内容</param>
        public static void WriteLog_LocalTxt(string log)
        {
            Task.Run(() =>
            {
                string filename = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                string filePath = AppDomain.CurrentDomain.BaseDirectory + "\\log";
                if (!Directory.Exists(filePath))
                    Directory.CreateDirectory(filePath);
                filePath = Path.Combine(filePath, filename);
                string logContent = $"{DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")}:{log}\r\n";
                File.AppendAllText(filePath, logContent);
            });
        }

        public static void WriteException(Exception ex)
        {
            WriteLog_LocalTxt(ex.Message);
        }

        private static void WriteException(string msg)
        {
            WriteLog_LocalTxt(msg);
        }

        public static void WriteLog(string msg)
        {
            WriteLog_LocalTxt(msg);
        }
    }
}
