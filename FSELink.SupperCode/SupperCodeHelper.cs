using FSELink.Bussiness;
using FSELink.Entities;
using FSELink.SupperCode.Common;
using FSELink.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSELink.SupperCode
{
    public partial class SupperCodeHelper
    {
         TraceCodesManager codesManager = new TraceCodesManager();
         RequestOrderManager orderManager = new RequestOrderManager();
         SystemConfigManager configManager = new SystemConfigManager();
         OrderFangWeiCodeRuleManager fangWeiCodeRuleManager = new OrderFangWeiCodeRuleManager();
         TraceCodeRuleManager traceCodeRuleManager = new TraceCodeRuleManager();
         TraceCodesManager traceCodesManager = new TraceCodesManager();
         object objLocke = new object();
        public  bool IsServerStart = false;

        #region 公共方法


        public async  Task<string> GenerateCodeAsync()
        {
            try
            {
                GeneratCodePara para = await GetGeneratorPara();
                if (para.RequestOrders.Count > 0)
                {
                    new Task(() => { GenerateByOrder(para); }).Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteException(ex);
            }
            return "";
        }

        public async  Task<string> ExportFileAsync()
        {
            try
            {
                GeneratCodePara para = await GetExportPara();
                if (para.RequestOrders.Count > 0)
                {
                    new Task(() => { ExportFileAsync(para); }).Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteException(ex);
            }
            return "";
        }


        public  async Task<string> SendDataToMSZZ()
        {
            List<RequestOrder> sendingOrders = await orderManager.GetList(t => t.WriteStatus == 1);
            if (sendingOrders.Count > 0) return "";
            GeneratCodePara sendOrders = await GetSendOrderPara();

            if (sendOrders.RequestOrders.Count() == 0) return "";
            GeneratCodePara sending = sendOrders.Clone();
            RequestOrder order = sending.RequestOrders[0].Clone();
            try
            {
                
                GeneratCodePara generatCodePara = new GeneratCodePara();
                generatCodePara.RequestOrders.Add(order);
                generatCodePara.GenerateConfig = sendOrders.GenerateConfig;
                order.WriteStatus = 1;
                await orderManager.Update(order);
                await SendDataToMSZZByOrder(order, sendOrders.GenerateConfig.DataInterface);
                generatCodePara = null;
            }
            catch (Exception ex)
            {
                order.WriteStatus =0 ;
                await orderManager.Update(order);
                LogHelper.WriteException(ex);
            }
            return "";
        }



        private async  Task<ReturnMessage> SendData(string strData,string strURL)
        {
            //参数对象
            HttpItem objHttpItem = new HttpItem()
            {
                URL = strURL,
                //Postdata = strData,
                KeepAlive = true,
                Encoding = "utf-8",
                Method = "POST",
                Referer = "",
                TimeOut=10000000
            };
            ReturnMessage message;
            //取Html
            try
            {
                HttpHelper helper = new HttpHelper();
                string html = helper.GetHtml(objHttpItem);
                message = JsonHelper.JsonToEntity<ReturnMessage>(html);
            }
            catch(Exception ex)
            {
                message = new ReturnMessage{ code = -1, msg = ex.Message,count=0 };
            }
            return message;
        }

        private async  Task<ReturnMessage> SendData(string strURL)
        {
            //参数对象
            HttpItem objHttpItem = new HttpItem()
            {
                URL = strURL,
                Postdata = "",
                Encoding = "utf-8",
                Method = "POST",
                Referer = "",
                TimeOut = 10000000
            };
            ReturnMessage message;
            //取Html
            try
            {
                HttpHelper helper = new HttpHelper();
                string html = helper.GetHtml(objHttpItem);
                message = JsonHelper.JsonToEntity<ReturnMessage>(html);
            }
            catch (Exception ex)
            {
                message = new ReturnMessage { code = -1, msg = ex.Message, count = 0 };
            }
            return message;
        }


        
     

        private async  Task<string> GetCode(Int64 codeIndex,List<TraceCodeRule> codeRule)
        {
            
            string strCode = "";
            lock(objLocke)
            {
            
                foreach (TraceCodeRule rule in codeRule)
                {
                    strCode+= ParagraphCode.GetCode(codeIndex, rule);
                }
            }
            return strCode;
        }


        private async  Task<string> GetCode(Int64 codeIndex, List<OrderFangWeiCodeRule> codeRule)
        {

            string strCode = "";
            //lock (objLocke)
            //{
                foreach (OrderFangWeiCodeRule rule in codeRule)
                {
                    strCode += await ParagraphCode.GetCode(codeIndex, rule);
                }
            //}
            return strCode;
        }

        #endregion




        #region 数据导出模块
        
        /// <summary>
        /// 获取导出数据的相关参数
        /// </summary>
        /// <returns></returns>
        private async  Task<GeneratCodePara> GetExportPara()
        {
            GeneratCodePara para = new GeneratCodePara();
            para.RequestOrders = await orderManager.GetList(t => t.OrderStatus == 3);
            if (para.RequestOrders.Count == 0) return para;
            List<SystemConfig> systemConfigs = await configManager.GetList();

            List<TraceCodeRule> TraceCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.TraceCodeRule.ToString());
            List<TraceCodeRule> BoxCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.BoxCodeRule.ToString());
            List<TraceCodeRule> DoCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.DoCodeRule.ToString());

            para.GenerateConfig = systemConfigs.Count < 1 ? null : systemConfigs[0];
            para.TraceCodeRule = TraceCodeRule;
            para.BoxCodeRule = BoxCodeRule;
            para.DoCodeRule = DoCodeRule;
            foreach (RequestOrder order in para.RequestOrders)
            {
                order.OrderStatus = 4;
            }
            if (para.RequestOrders.Count > 0) await orderManager.Update(para.RequestOrders);

            return para;
        }





        private async  Task<bool> ExportFileAsync(GeneratCodePara para)
        {
            GeneratCodePara generatCodePara = new GeneratCodePara();
            bool blSucess = false;
            try
            {
                foreach (RequestOrder order in para.RequestOrders)
                {
                    if (!IsServerStart) break;
                    generatCodePara = new GeneratCodePara();
                    generatCodePara.RequestOrders.Add(order);
                    generatCodePara.TraceCodeRule = para.TraceCodeRule;
                    generatCodePara.BoxCodeRule = para.BoxCodeRule;
                    generatCodePara.DoCodeRule = para.DoCodeRule;
                    generatCodePara.OrderBoxFWRules = para.OrderBoxFWRules;
                    generatCodePara.OrderTraceFWRules = para.OrderTraceFWRules;
                    generatCodePara.GenerateConfig = para.GenerateConfig;
                    await ExportOrderDataToFile(generatCodePara);

                }
                if (!IsServerStart)
                {
                    blSucess = IsServerStart;
                    foreach (RequestOrder order in para.RequestOrders) order.OrderStatus = 3;
                    await orderManager.Update(para.RequestOrders);
                }
                else
                    blSucess = true;

            }
            catch (Exception ex)
            {
                LogHelper.WriteException(ex);
                blSucess = false;
            }
            return blSucess;

        }



        private async  Task<bool> ExportOrderDataToFile(GeneratCodePara para)
        {
            RequestOrder order = para.RequestOrders[0];
            List<TraceCodes> traceCodes;
            List<TraceCodes> boxCodes;
            List<TraceCodes> duoCodes;
            List<TraceCodes> OrderCodes;
            string strUrl = "";
            StringBuilder sb = new StringBuilder();
            StringBuilder sbDuo = new StringBuilder(), sbBox = new StringBuilder();
            bool blTemp = false;
            int duoCount = 0, boxCount = 0, traceCodeCount = 0;
            StreamWriter sr;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string strSavedirectory = order.CustName + "(" + order.OrderNo + ")_" + order.TotalCount;
            string strSavePath = SystemInfo.DataExportPath + "\\" + strSavedirectory;
            if (!Directory.Exists(strSavePath))
                Directory.CreateDirectory(strSavePath);

            if (ParagraphTransform.GetOrderType(order.OrderType) == OrderType.XM) strUrl = para.GenerateConfig.XMURL;
            else strUrl = para.GenerateConfig.MSZZURL;
            if (strUrl.IndexOf("?code=") < 0)
                strUrl += "?code=";

            OrderCodes = await codesManager.GetList(t => t.OrderId == order.Id);
            if (ParagraphTransform.GetCodeType(para.RequestOrders[0].CodeType) == CodeType.SetLabel)
            {
                string[] BatchRation = order.BatchRatio.Split(':');

                int DuoCount = int.Parse(BatchRation[0]);
                int BoxCountOfPerDuo = int.Parse(BatchRation[1]);
                int ProductCountOfPerBox = int.Parse(BatchRation[2]);

                //if(DuoCount> 0)
                //{
                //    duoCount = DuoCount / DuoCount * order.OrderCount;
                //    boxCount = (BoxCountOfPerDuo / DuoCount) * order.OrderCount;
                //    traceCodeCount=(ProductCountOfPerBox/DuoCount)* order.OrderCount;
                //}
                //else
                //{
                //    duoCount = OrderCodes.Where(t => t.CodeType == "2").Count();
                //    boxCount = OrderCodes.Where(t => t.CodeType == "1").Count();
                //    traceCodeCount = OrderCodes.Where(t=>t.CodeType=="0").Count();
                //}

                duoCount = OrderCodes.Where(t => t.CodeType == "2").Count();
                boxCount = OrderCodes.Where(t => t.CodeType == "1").Count();
                traceCodeCount = OrderCodes.Where(t => t.CodeType == "0").Count();
                traceCodes = OrderCodes.Where(t => t.CodeType == "0").ToList();

                string temp = "";
                temp = "产品码";
                if (order.IsTraceCodeFW.ToUpper() == "Y")
                    temp += ",防伪密码";
                temp += ",箱/包码";
                if (order.IsBoxCodeFW.ToUpper() == "Y")
                    temp += ",箱/包防伪密码";

                // duoCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType=="2");

                int boxCodeLenght = traceCodes[0].BoxCode.Length;
                int traceCodeLenght = traceCodes[0].Barcode.Length+strUrl.Length;
                int fwcodeLenght= traceCodes[0].FwCode.Length;
                int boxcodefwLenght = traceCodes[0].BoxFwCode.Length;
                int duoCodeLenght= traceCodes[0].DuoCode.Length;
                string strtemp = "产品码";
                string strTitle = "";
                if (traceCodeLenght > strtemp.Length+2)
                {
                    int padlen = traceCodeLenght/2+3;
                    strTitle = "|" + strtemp.PadLeft(padlen, '-');
                    strTitle += strtemp.PadRight((strTitle.Length+ strtemp.Length+2), '-').Replace(strtemp, "") + "|";
                }
                else
                    strTitle = "|" + strtemp + "|";
                if (order.IsTraceCodeFW.ToUpper() == "Y")
                {
                    strtemp = "防伪码(产品)";
                    if (fwcodeLenght > strtemp.Length + 2)
                    {
                        int padlen = fwcodeLenght/ 2 - 1;
                        strTitle += strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight(padlen+ strtemp.Length, '-').Replace(strtemp, "") + "|";
                    }
                    else
                        strTitle +=  strtemp + "|";
                }

                if (boxCodeLenght > 10)
                {
                    strtemp = "张码";
                    if (boxCodeLenght > strtemp.Length + 2)
                    {
                        int padlen = boxCodeLenght / 2 ;
                        strTitle += strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight(padlen+ strtemp.Length, '-').Replace(strtemp, "") + "|";
                    }
                    else
                        strTitle += strtemp + "|";
                }

                if (order.IsBoxCodeFW.ToUpper() == "Y")
                {
                    strtemp = "防伪码(张)";
                    if (boxcodefwLenght > (strtemp.Length + 2))
                    {
                        int padlen = (boxcodefwLenght - strtemp.Length) / 2 - 1;
                        strTitle += strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight(padlen+ strtemp.Length, '-').Replace(strtemp,"") + "|";
                    }
                    else
                        strTitle += strtemp + "|";
                }


                strtemp = "包码";
                if (duoCodeLenght > 5)
                {
                    if (duoCodeLenght > strtemp.Length + 2)
                    {
                        int padlen = (duoCodeLenght - strtemp.Length) / 2 - 1;
                        strTitle += strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight(padlen+strtemp.Length, '-').Replace(strtemp, "") + "|";
                    }
                    else
                        strTitle += strtemp + "|";
                }
               
                sb.AppendLine(strTitle);



                duoCodes = OrderCodes.Where(t => t.CodeType == "2").ToList();
                if (duoCodes.Count > 0)
                {
                    sbDuo = new StringBuilder();
                    sbBox = new StringBuilder();

                    //temp += ",垛码";
                    //sb.AppendLine(temp);
                    //sbDuo.AppendLine("垛码");
                    //temp = "箱码";
                    //if (order.IsBoxCodeFW.ToUpper() == "Y")
                    //    temp += ",箱码密码";
                    //temp += ",垛码";

                    //sbBox.AppendLine(temp);
                    foreach (TraceCodes duo in duoCodes)
                    {
                        //boxCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "1" && t.DuoCode==duo.Barcode );
                        boxCodes = OrderCodes.Where(t => t.CodeType == "1" && t.DuoCode == duo.Barcode).ToList();
                        //sbDuo.AppendLine(duo.Barcode);
                        foreach (TraceCodes box in boxCodes)
                        {
                            //traceCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "0" && t.BoxCode==box.Barcode );
                            traceCodes = OrderCodes.Where(t => t.CodeType == "0" && t.BoxCode == box.Barcode && t.DuoCode == duo.Barcode).ToList();
                            //if (order.IsBoxCodeFW.ToUpper() == "Y")
                            //    sbBox.AppendLine(strUrl + box.Barcode + "," + box.FwCode + "," + duo.Barcode);
                            //else
                            //    sbBox.AppendLine(box.Barcode + "," + duo.Barcode);
                            foreach (TraceCodes code in traceCodes)
                            {
                                temp = strUrl + code.Barcode;
                                if (order.IsTraceCodeFW.ToUpper() == "Y")
                                    temp += "," + code.FwCode;
                                if (order.IsBoxCodeFW.ToUpper() == "Y")
                                    temp += "," + strUrl + box.Barcode + "," + box.FwCode;
                                else
                                    temp += "," + box.Barcode;
                                temp += "," + code.DuoCode;
                                sb.AppendLine(temp);
                            }
                            if (!IsServerStart) return false;
                        }
                    }
                }
                else
                {
                    //boxCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "1" );
                    //sb.AppendLine(temp);
                    sbBox = new StringBuilder();
                    strTitle = "";
                    if (boxCodeLenght > 10)
                    {
                        strtemp = "张码";
                        if (boxCodeLenght > strtemp.Length + 2)
                        {
                            int padlen = boxCodeLenght / 2;
                            strTitle = "|" + strtemp.PadLeft(padlen, '-');
                            strTitle += strtemp.PadRight(padlen + strtemp.Length, '-').Replace(strtemp, "") + "|";
                        }
                        else
                            strTitle +="|"+ strtemp + "|";
                    }

                    if (order.IsBoxCodeFW.ToUpper() == "Y")
                    {
                        strtemp = "防伪码(张)";
                        if (boxcodefwLenght > (strtemp.Length + 2))
                        {
                            int padlen = (boxcodefwLenght - strtemp.Length) / 2 - 1;
                            strTitle += strtemp.PadLeft(padlen, '-');
                            strTitle += strtemp.PadRight(padlen + strtemp.Length, '-').Replace(strtemp, "") + "|";
                        }
                        else
                            strTitle += strtemp + "|";
                    }
                    sbBox.AppendLine(strTitle);
                    boxCodes = OrderCodes.Where(t => t.CodeType == "1").ToList();
                    foreach (TraceCodes box in boxCodes)
                    {
                        // traceCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "0" && t.BoxCode == box.BoxCode);
                        traceCodes = OrderCodes.Where(t => t.CodeType == "0" && t.BoxCode == box.Barcode).ToList();
                        if (order.IsBoxCodeFW.ToUpper() == "Y")
                            sbBox.AppendLine(strUrl + box.Barcode + "," + box.FwCode);
                        else
                            sbBox.AppendLine(box.Barcode);
                        foreach (TraceCodes code in traceCodes)
                        {
                            temp = strUrl + code.Barcode;
                            if (order.IsTraceCodeFW.ToUpper() == "Y")
                                temp += "," + code.FwCode;
                            if (order.IsBoxCodeFW.ToUpper() == "Y")
                                temp += "," + strUrl + code.BoxCode + "," + box.FwCode;
                            else
                                temp += "," + code.BoxCode;
                            sb.AppendLine(temp);
                        }
                        if (!IsServerStart) return false;
                    }

                }


                string TraceCodefileName = order.CustName + "(" + order.OrderNo + ")_产品码_" + traceCodeCount + ".txt";
                //string BoxCodefileName = order.CustName + "(" + order.OrderNo + ")_箱码_" + boxCount + "_.txt";
                //string DuoCodefileName = order.CustName + "(" + order.OrderNo + ")_垛码_" + duoCount + ".txt";

                TraceCodefileName = strSavePath + "\\" + TraceCodefileName;
                sr = File.AppendText(TraceCodefileName);
                sr.Write(sb.ToString());
                sr.Close();
                //if (sbBox.Length > 0)
                //{
                //    BoxCodefileName = strSavePath + "\\" + BoxCodefileName;
                //    sr = File.AppendText(BoxCodefileName);
                //    sr.Write(sbBox.ToString());
                //    sr.Close();
                //}
                //if (sbDuo.Length > 0)
                //{
                //    DuoCodefileName = strSavePath + "\\" + DuoCodefileName;
                //    sr = File.AppendText(DuoCodefileName);
                //    sr.Write(sbDuo.ToString());
                //    sr.Close();
                //}

            }
            else
            {
                sb = new StringBuilder();
                boxCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "1");
                traceCodes = await codesManager.GetList(t => t.OrderId == order.Id && t.CodeType == "0");
                int boxCodeLenght = 0; 
                int boxcodefwLenght = 0;
                int traceCodeLenght = 0;
                int fwcodeLenght = 0;

                if (boxCodes.Count() > 0)
                {
                    boxCodeLenght = boxCodes[0].Barcode.Length;
                    boxcodefwLenght = boxCodes[0].BoxFwCode.Length;
                }
                if (traceCodes.Count() > 0)
                {
                    traceCodeLenght = traceCodes[0].Barcode.Length + strUrl.Length;
                    fwcodeLenght = traceCodes[0].FwCode.Length;
                    boxcodefwLenght = traceCodes[0].BoxFwCode.Length;
                }
                string strtemp = "张码";
                string strTitle = "";
                string fileName = "";
                if (boxCodes.Count > 0)
                {
                    if (boxCodeLenght > strtemp.Length)
                    {
                        int padlen = boxCodeLenght / 2 + 3;
                        strTitle = "|" + strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight((strTitle.Length + strtemp.Length + 2), '-').Replace(strtemp, "") + "|";
                    }
                    else
                        strTitle = "|" + strtemp + "|";
                    if (order.IsBoxCodeFW.ToUpper() == "Y")
                    {
                        boxCodeLenght = boxCodes[0].FwCode.Length;
                        strtemp = "防伪码(张)";
                        if (boxCodeLenght > (strtemp.Length + 2))
                        {
                            int padlen = (boxCodeLenght - strtemp.Length) / 2;
                            strTitle += strtemp.PadLeft(padlen, '-');
                            strTitle += strtemp.PadRight(padlen + strtemp.Length, '-').Replace(strtemp, "") + "|";
                        }
                        else
                            strTitle += strtemp + "|";
                    }
                    sb.AppendLine(strTitle);
                    foreach (TraceCodes code in boxCodes)
                    {
                        if (order.IsBoxCodeFW.ToUpper() == "Y")
                            strtemp = strUrl + code.Barcode + "," + code.FwCode;
                        else
                            strtemp = code.Barcode;
                        sb.AppendLine(strtemp);
                    }
                    if (!IsServerStart) return false;
                    // string fileName = order.CustName + "(" + order.OrderNo + ")_箱码_" + boxCodes.Count + ".txt";
                    fileName = order.CustName + "(" + order.OrderNo + ")_箱码_" + boxCodes.Count() + ".txt";
                    fileName = strSavePath + "\\" + fileName;
                    sr = File.AppendText(fileName);
                    sr.Write(sb.ToString());
                    sr.Close();

                }

                if (traceCodes.Count > 0)
                {
                    sb = new StringBuilder();
                    strtemp = "产品码";
                    // traceCodeLenght = traceCodeLenght + strUrl.Length;
                    if (traceCodeLenght > strtemp.Length)
                    {
                        int padlen = traceCodeLenght / 2 + 2;
                        strTitle = "|" + strtemp.PadLeft(padlen, '-');
                        strTitle += strtemp.PadRight((strTitle.Length + strtemp.Length + 2), '-').Replace(strtemp, "") + "|";
                    }
                    else
                        strTitle = "|" + strtemp + "|";




                    if (order.IsTraceCodeFW.ToUpper() == "Y")
                    {

                        traceCodeLenght = traceCodes[0].FwCode.Length;
                        strtemp = "防伪码(产品)";
                        if (traceCodeLenght > (strtemp.Length + 2))
                        {
                            int padlen = (boxCodeLenght - strtemp.Length) / 2 - 1;
                            strTitle += strtemp.PadLeft(padlen, '-');
                            strTitle += strtemp.PadRight(padlen + strtemp.Length, '-').Replace(strtemp, "") + "|";
                        }
                        else
                            strTitle += strtemp + "|";
                    }
                    sb.AppendLine(strTitle);
                    foreach (TraceCodes code in traceCodes)
                    {
                        strtemp = strUrl + code.Barcode;
                        if (order.IsTraceCodeFW.ToUpper() == "Y") strtemp += "," + code.FwCode;
                        sb.AppendLine(strtemp);
                    }
                    if (!IsServerStart) return false;
                    // fileName = order.CustName + "(" + order.OrderNo + ")_产品码_" + traceCodes.Count + ".txt";
                    fileName = order.CustName + "(" + order.OrderNo + ")_产品码_" + traceCodes.Count() + ".txt";
                    fileName = strSavePath + "\\" + fileName;
                    sr = File.AppendText(fileName);
                    sr.Write(sb.ToString());
                    sr.Close();
                }

            }



            string zipfilename = SystemInfo.DataExportPath + "\\" + strSavedirectory + ".zip";
            if (File.Exists(zipfilename))
                File.Delete(zipfilename);
            if (para.GenerateConfig.ZipPassword.Trim().Length > 0)
                ZipHelper.ZipDirectory(strSavePath, zipfilename, para.GenerateConfig.ZipPassword);
            else
                ZipHelper.ZipDirectory(strSavePath, zipfilename);
            foreach (string file in Directory.GetFiles(strSavePath)) File.Delete(file);
            Directory.Delete(strSavePath);
            order.DownLoadFile = zipfilename;
            order.ZipPassword = para.GenerateConfig.ZipPassword;
            order.OrderStatus = 5;
            await orderManager.Update(order);
            blTemp = true;

            LogHelper.WriteLog("订单号：" + order.OrderNo + "   数据导出所花时间：" + Convert.ToDouble((stopwatch.ElapsedMilliseconds * 1.00 / 1000)) + "秒");
            stopwatch = null;
            return blTemp;
        }



        #endregion


        #region 生码模块
      
        /// <summary>
        /// 获取生成数据的相关参数
        /// </summary>
        /// <returns></returns>
        private async  Task<GeneratCodePara> GetGeneratorPara()
        {


            GeneratCodePara para = new GeneratCodePara();
            para.RequestOrders = await orderManager.GetList(t => t.OrderStatus == 1);
            if (para.RequestOrders.Count == 0) return para;
            List<SystemConfig> systemConfigs = await configManager.GetList();

            List<TraceCodeRule> TraceCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.TraceCodeRule.ToString());
            List<TraceCodeRule> BoxCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.BoxCodeRule.ToString());
            List<TraceCodeRule> DoCodeRule = await traceCodeRuleManager.GetList(t => t.CodeType == RuleType.DoCodeRule.ToString());

            para.GenerateConfig = systemConfigs.Count < 1 ? null : systemConfigs[0];
            para.TraceCodeRule = TraceCodeRule;
            para.BoxCodeRule = BoxCodeRule;
            para.DoCodeRule = DoCodeRule;

            foreach (RequestOrder order in para.RequestOrders)
            {
                order.OrderStatus = 2;
            }
            if (para.RequestOrders.Count > 0) await orderManager.Update(para.RequestOrders);
            return para;
        }

        private async  Task<bool> GenerateByOrder(GeneratCodePara para)
        {
            GeneratCodePara generatCodePara = new GeneratCodePara();
            bool blSucess = false;
            foreach (RequestOrder order in para.RequestOrders)
            {
                generatCodePara = new GeneratCodePara();
                generatCodePara.RequestOrders.Add(order);
                generatCodePara.TraceCodeRule = para.TraceCodeRule;
                generatCodePara.BoxCodeRule = para.BoxCodeRule;
                generatCodePara.DoCodeRule = para.DoCodeRule;
                generatCodePara.OrderBoxFWRules = para.OrderBoxFWRules;
                generatCodePara.OrderTraceFWRules = para.OrderTraceFWRules;
                generatCodePara.GenerateConfig = para.GenerateConfig;
                generatCodePara.OrderTraceFWRules = await fangWeiCodeRuleManager.GetList(t => t.OrderID == order.Id);
                generatCodePara.OrderBoxFWRules = await fangWeiCodeRuleManager.GetList(t => t.OrderID == order.Id);
                await CreateCodByOneOrderAsync(generatCodePara);


            }
            if (!IsServerStart)
            {
                foreach (RequestOrder order in para.RequestOrders)
                {
                    order.OrderStatus = 1;
                    await traceCodesManager.Delete(t => t.OrderId == order.Id);
                }
                await orderManager.Update(para.RequestOrders);
                blSucess = false;
            }
            else
                blSucess = true;

            return blSucess;

        }



        private async  Task<bool> CreateSendDataFile(List<TraceCodes> lstcodes, GeneratCodePara para)
        {

            long codeCount = 0;
            List<SupperCodeEntity> supperCodes = new List<SupperCodeEntity>();
            Stopwatch stopwatch = new Stopwatch();
            string strData = "[";
            foreach (TraceCodes code in lstcodes)
            {
                codeCount++;
                supperCodes.Add(new SupperCodeEntity
                {
                    barcode = code.Barcode,
                    fwcode = code.FwCode,
                    boxcode = code.BoxCode,
                    boxsecretcode = code.BoxFwCode,
                    pagecode = code.DuoCode,
                    codetype = code.CodeType,
                    orderno = code.OrderId.ToString()
                });
            }


            strData = JsonHelper.ObjectToJSON(supperCodes);
            if (!Directory.Exists(SystemInfo.SendDataPath))
                Directory.CreateDirectory(SystemInfo.SendDataPath);
            string fileName = SystemInfo.SendDataPath + "\\" + para.SucessOrders[0].Id + ".txt";
            string zipFilename = para.SucessOrders[0].Id.ToString();
            File.AppendAllText(fileName, strData);
            ZipHelper.ZipFile(fileName, SystemInfo.SendDataPath + "\\" + zipFilename + ".zip");
            File.Delete(fileName);
            return false;
        }


        private async  Task<bool> CreateCodByOneOrderAsync(GeneratCodePara para)
        {
            bool blTemp = false;
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                List<TraceCodes> traceCodes = await CreateCodeAsync(para);
                if (traceCodes.Count > 0)
                {
                    codesManager.SqlBulkCopy(traceCodes);
                    if (SystemInfo.AutoSendData) await CreateSendDataFile(traceCodes, para);
                    if (para.SucessOrders.Count > 0) await orderManager.Update(para.SucessOrders);
                    if (para.ErrorOrders.Count > 0) await orderManager.Update(para.ErrorOrders);
                  
                    foreach (string str in para.ErrorMessages) LogHelper.WriteException(new Exception(str));
                    LogHelper.WriteLog("订单号：" + para.RequestOrders[0].OrderNo + "   发布数量所花时间：" + Convert.ToDouble(stopwatch.ElapsedMilliseconds * 1.000 / 1000) + "秒");
                }
                stopwatch = null;
                blTemp = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteException(ex);
                foreach (RequestOrder order in para.RequestOrders) order.OrderStatus = 1;
                await orderManager.Update(para.RequestOrders);
                blTemp = false;
            }
            return blTemp;
        }

        private async  Task<List<TraceCodes>> CreateCodeAsync(GeneratCodePara para)
        {
            List<TraceCodes> traceCodes = new List<TraceCodes>();

            DataTable DtCodes = new DataTable();
            DtCodes.Columns.Add(new DataColumn("Barcode"));
            DtCodes.PrimaryKey = new DataColumn[] { DtCodes.Columns["Barcode"] };
            foreach (RequestOrder Order in para.RequestOrders)
            {
                if (Order.CodeType.Trim() == CodeType.ScatterLabel.ToString()) //生成散标
                {
                    Int64 traceCodeCount = Convert.ToInt64(Order.TraceCodeCount + Math.Ceiling(Order.TraceCodeCount* para.GenerateConfig.LoseRatio * 0.01)+ Math.Ceiling(Order.TraceCodeCount * Order.LossRate * 0.01));
                    Int64 boxCodeCount = Convert.ToInt64(Order.BoxCodeCount + Math.Ceiling(Order.BoxCodeCount * para.GenerateConfig.LoseRatio * 0.01) + Math.Ceiling(Order.BoxCodeCount * Order.LossRate * 0.01));
                    string strTraceCode = "", strFwCode = "", strBoxCode = "", strBoxFwCode = "", strDoCode = "";
                    #region 生产产品码
                    for (Int64 ind = 0; ind < traceCodeCount; ind++)
                    {
                        do
                        {
                            strTraceCode = await GetCode(ind, para.TraceCodeRule);
                        } while (DtCodes.Select("Barcode='" + strTraceCode + "'").Count() > 0);

                        if (Order.IsTraceCodeFW == "Y" && para.OrderTraceFWRules != null)
                            strFwCode = await GetCode(ind, para.OrderTraceFWRules);
                        traceCodes.Add(new TraceCodes
                        {
                            Barcode = strTraceCode,
                            FwCode = strFwCode,
                            BoxCode = strBoxCode,
                            BoxFwCode = strBoxFwCode,
                            DuoCode = strDoCode,
                            OrderId = Order.Id,
                            Year = DateTime.Now.Year,
                            Month = DateTime.Now.Month,
                            CreateDate = DateTime.Now,
                            ModifyDate = DateTime.Now,
                            Createby = "Admin",
                            ModifyBy = "Admin",
                            CodeType = "0"
                        });
                        DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strTraceCode);

                        #region 服务停止是退出发布数据
                        if (!IsServerStart)
                        {
                            traceCodes.Clear();
                            return traceCodes;
                        }
                        #endregion

                    }
                    #endregion
                    #region 生成箱码
                    for (Int64 ind = 0; ind < boxCodeCount; ind++)
                    {
                        do
                        {
                            strTraceCode = await GetCode(ind, para.BoxCodeRule);
                        } while (DtCodes.Select("Barcode='" + strTraceCode + "'").Count() > 0);

                        if (Order.IsTraceCodeFW == "Y" && para.OrderTraceFWRules != null)
                            strFwCode = await GetCode(ind, para.OrderTraceFWRules);
                        traceCodes.Add(new TraceCodes
                        {
                            Barcode = strTraceCode,
                            FwCode = strFwCode,
                            BoxCode = strBoxCode,
                            BoxFwCode = strBoxFwCode,
                            DuoCode = strDoCode,
                            OrderId = Order.Id,
                            Year = DateTime.Now.Year,
                            Month = DateTime.Now.Month,
                            CreateDate = DateTime.Now,
                            ModifyDate = DateTime.Now,
                            Createby = "Admin",
                            ModifyBy = "Admin",
                            CodeType = "1"
                        });
                        DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strTraceCode);
                        #region 服务停止是退出发布数据
                        if (!IsServerStart)
                        {
                            traceCodes.Clear();
                            return traceCodes;
                        }
                        #endregion

                    }
                    #endregion
                    Order.OrderStatus = 3;
                    /// Order.OrderAmount = (float)Math.Round(Order.OrderAmount, 2);
                    para.SucessOrders.Add(Order);
                }
                else if (Order.CodeType.Trim() == CodeType.SetLabel.ToString().Trim()) //生套标签
                {
                    string[] BatchRation = Order.BatchRatio.Split(':');

                    int DuoCount = int.Parse(BatchRation[0]);
                    int BoxCountOfPerDuo = int.Parse(BatchRation[1]);
                    int ProductCountOfPerBox = int.Parse(BatchRation[2]);
                    Int64 OrderCount = Convert.ToInt64(Order.OrderCount + Math.Ceiling(Order.OrderCount * para.GenerateConfig.LoseRatio * 0.01) +  Math.Ceiling( Order.OrderCount * (Order.LossRate * 0.01)));
                    Int64 boxCodeCount = Convert.ToInt64(Order.BoxCodeCount + Math.Ceiling(Order.BoxCodeCount * para.GenerateConfig.LoseRatio * 0.01) + Math.Ceiling(Order.BoxCodeCount * Order.LossRate * 0.01));
                    string strTraceCode = "", strFwCode = "", strBoxCode = "", strBoxFwCode = "", strDoCode = "";


                    if (DuoCount > 1)
                    {
                        BoxCountOfPerDuo = BoxCountOfPerDuo / DuoCount;
                        ProductCountOfPerBox = ProductCountOfPerBox / DuoCount;
                        DuoCount = DuoCount / DuoCount;
                        if (BoxCountOfPerDuo < 1)
                        {
                            Order.OrderStatus = 1;
                            //Order.OrderAmount = (float)Math.Round(Order.OrderAmount, 2);
                            para.ErrorOrders.Add(Order);
                            para.ErrorMessages.Add("包装比例错误(订单号：" + Order.OrderNo + "---" + Order.BatchRatio + ")：垛码比例大于0时，箱码数量必须大于0");
                            continue;
                        }
                        if (DuoCount > BoxCountOfPerDuo)
                        {
                            Order.OrderStatus = 1;
                            //Order.OrderAmount = (float)Math.Round(Order.OrderAmount, 2);
                            para.ErrorOrders.Add(Order);
                            para.ErrorMessages.Add("包装比例错误(订单号：" + Order.OrderNo + "---" + Order.BatchRatio + ")：包装比例中箱码数量必须小于垛数量");
                            continue;
                        }
                    }
                    if (BoxCountOfPerDuo < 1)
                    {
                        Order.OrderStatus = 1;
                        //Order.OrderAmount = (float)Math.Round(Order.OrderAmount, 2);
                        para.ErrorOrders.Add(Order);
                        para.ErrorMessages.Add("包装比例错误(订单号：" + Order.OrderNo + "---" + Order.BatchRatio + ")：包装比例中箱码数量大于0");
                        continue;
                    }
                    ProductCountOfPerBox = ProductCountOfPerBox / BoxCountOfPerDuo;


                    #region 生成套标
                    for (Int64 ind = 0; ind < OrderCount; ind++)
                    {


                        if (DuoCount > 0)
                        {

                            do
                            {
                                strDoCode = await GetCode(ind, para.DoCodeRule);
                            } while (DtCodes.Select("Barcode='" + strDoCode + "'").Count() > 0);

                            traceCodes.Add(new TraceCodes
                            {
                                Barcode = strDoCode,
                                FwCode = "",
                                BoxCode = "",
                                BoxFwCode = "",
                                DuoCode = "",
                                OrderId = Order.Id,
                                Year = DateTime.Now.Year,
                                Month = DateTime.Now.Month,
                                CreateDate = DateTime.Now,
                                ModifyDate = DateTime.Now,
                                Createby = "Admin",
                                ModifyBy = "Admin",
                                CodeType = "2"
                            });
                            DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strDoCode);


                            for (int ind1 = 0; ind1 < BoxCountOfPerDuo; ind1++)
                            {
                                do
                                {
                                    strBoxCode = await GetCode(ind, para.BoxCodeRule);
                                } while (DtCodes.Select("Barcode='" + strBoxCode + "'").Count() > 0);

                                if (Order.IsBoxCodeFW == "Y" && para.OrderBoxFWRules != null)
                                    strBoxFwCode = await GetCode(ind, para.OrderBoxFWRules);
                                traceCodes.Add(new TraceCodes
                                {
                                    Barcode = strBoxCode,
                                    FwCode = strBoxFwCode,
                                    BoxCode = "",
                                    BoxFwCode = "",
                                    DuoCode = strDoCode,
                                    OrderId = Order.Id,
                                    Year = DateTime.Now.Year,
                                    Month = DateTime.Now.Month,
                                    CreateDate = DateTime.Now,
                                    ModifyDate = DateTime.Now,
                                    Createby = "Admin",
                                    ModifyBy = "Admin",
                                    CodeType = "1"
                                });
                                DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strBoxCode);

                                //if (BoxCountOfPerDuo > 1)
                                //    ProductCountOfPerBox = ProductCountOfPerBox / BoxCountOfPerDuo;

                                for (int ind2 = 0; ind2 < ProductCountOfPerBox; ind2++)
                                {
                                    do
                                    {
                                        strTraceCode = await GetCode(ind, para.TraceCodeRule);
                                    } while (DtCodes.Select("Barcode='" + strTraceCode + "'").Count() > 0);

                                    if (Order.IsTraceCodeFW == "Y" && para.OrderTraceFWRules != null)
                                        strFwCode = await GetCode(ind, para.OrderTraceFWRules);
                                    traceCodes.Add(new TraceCodes
                                    {
                                        Barcode = strTraceCode,
                                        FwCode = strFwCode,
                                        BoxCode = strBoxCode,
                                        BoxFwCode = strBoxFwCode,
                                        DuoCode = strDoCode,
                                        OrderId = Order.Id,
                                        Year = DateTime.Now.Year,
                                        Month = DateTime.Now.Month,
                                        CreateDate = DateTime.Now,
                                        ModifyDate = DateTime.Now,
                                        Createby = "Admin",
                                        ModifyBy = "Admin",
                                        CodeType = "0"
                                    });
                                    DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strTraceCode);
                                }

                                #region 服务停止是退出发布数据
                                if (!IsServerStart)
                                {
                                    traceCodes.Clear();
                                    return traceCodes;
                                }
                                #endregion

                            }
                        }
                        else
                        {
                            //if (BoxCountOfPerDuo > 1)
                            //{
                            //    ProductCountOfPerBox = ProductCountOfPerBox / BoxCountOfPerDuo;
                            //    BoxCountOfPerDuo = BoxCountOfPerDuo / BoxCountOfPerDuo;
                            //}
                            do
                            {
                                strBoxCode = await GetCode(ind, para.BoxCodeRule);
                            } while (DtCodes.Select("Barcode='" + strBoxCode + "'").Count() > 0);
                            if (Order.IsBoxCodeFW == "Y" && para.OrderBoxFWRules != null)
                                strBoxFwCode = await GetCode(ind, para.OrderBoxFWRules);
                            traceCodes.Add(new TraceCodes
                            {
                                Barcode = strBoxCode,
                                FwCode = strBoxFwCode,
                                BoxCode = "",
                                BoxFwCode = "",
                                DuoCode = "",
                                OrderId = Order.Id,
                                Year = DateTime.Now.Year,
                                Month = DateTime.Now.Month,
                                CreateDate = DateTime.Now,
                                ModifyDate = DateTime.Now,
                                Createby = "Admin",
                                ModifyBy = "Admin",
                                CodeType = "1"
                            });
                            DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strBoxCode);
                            #region 服务停止是退出发布数据
                            if (!IsServerStart)
                            {
                                traceCodes.Clear();
                                return traceCodes;
                            }
                            #endregion

                            for (int ind2 = 0; ind2 < ProductCountOfPerBox; ind2++)
                            {
                                do
                                {
                                    strTraceCode = await GetCode(ind, para.TraceCodeRule);
                                } while (DtCodes.Select("Barcode='" + strTraceCode + "'").Count() > 0);

                                if (Order.IsTraceCodeFW == "Y" && para.OrderTraceFWRules != null)
                                    strFwCode = await GetCode(ind2, para.OrderTraceFWRules);
                                traceCodes.Add(new TraceCodes
                                {
                                    Barcode = strTraceCode,
                                    FwCode = strFwCode,
                                    BoxCode = strBoxCode,
                                    BoxFwCode = strBoxFwCode,
                                    DuoCode = "",
                                    OrderId = Order.Id,
                                    Year = DateTime.Now.Year,
                                    Month = DateTime.Now.Month,
                                    CreateDate = DateTime.Now,
                                    ModifyDate = DateTime.Now,
                                    Createby = "Admin",
                                    ModifyBy = "Admin",
                                    CodeType = "0"
                                });
                                DtCodes.Rows.Add(DtCodes.NewRow()["Barcode"] = strTraceCode);
                                #region 服务停止是退出发布数据
                                if (!IsServerStart)
                                {
                                    traceCodes.Clear();
                                    return traceCodes;
                                }
                                #endregion

                            }


                        }

                    }
                    #endregion

                    Order.OrderStatus = 3;
                    // Order.OrderAmount = (float)Math.Round(Order.OrderAmount, 2);
                    para.SucessOrders.Add(Order);
                }
            }
            #region 服务停止是退出发布数据
            if (!IsServerStart)
                traceCodes.Clear();

            #endregion


            return traceCodes;
        }



        #endregion



        #region 同步数据至码上增值
       
        /// <summary>
        /// 获取生成数据的相关参数
        /// </summary>
        /// <returns></returns>
        private  async  Task<GeneratCodePara> GetSendOrderPara()
        {
            GeneratCodePara para = new GeneratCodePara();
            para.RequestOrders = await orderManager.GetList(t => t.OrderStatus >=3 && t.WriteStatus == 0);
            if (para.RequestOrders.Count == 0) return para;
            List<SystemConfig> systemConfigs = await configManager.GetList();
            para.GenerateConfig = systemConfigs.Count < 1 ? null : systemConfigs[0];
            return para;
        }


        private  async Task<bool> SendDataToMSZZByOrder(RequestOrder order,string strInterface)
        {
            //if (para.RequestOrders.Count == 0) return false;
            ReturnMessage message = new ReturnMessage();
            try
            {
                string dataInterface = strInterface + "?filename=" + order.Id + "&orderid=" + order.Id;
                message = await SendData("", dataInterface);

                // LogHelper.WriteLog("订单号：" + para.SucessOrders[0].OrderNo + "-[" + message.msg + "] 同步数量：" + message.count);
            }
            catch (Exception ex)
            {
                message.msg = ex.Message;
            }
            LogHelper.WriteLog("订单号：" + order.OrderNo + "[" + message.msg + "] 同步数量：" + message.count);
            return true;
        }



        #endregion



    }
}
