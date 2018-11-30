using System;
using System.Drawing;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace EslUdpTest
{
    public class SmcEsl
    {
        public static int msg_ScanDevice = 0;
        public static int msg_ConnectEslDevice = 1;
        public static int msg_DisconnectEslDevice = 2;
        public static int msg_ReadEslName = 3;
        public static int msg_WriteEslName = 4;
        public static int msg_SetEslTurnPageTime = 5;
        public static int msg_WriteEslData = 6;
        public static int msg_WriteEslDataFinish = 7;
        public static int msg_WriteBeacon = 8;
        public static int msg_WriteEslDataTimeOut = 9;
        public static int msg_ConnectBleTimeOut = 10;

        //AP
        public static int msg_WriteESLDataBuffer = 11;
        public static int msg_UpdataESLDataFromBuffer = 12;
        public static int msg_SetRTCTime = 13;
        public static int msg_GetRTCTime = 14;
        public static int msg_SetBeaconTime = 15;

        public static int msg_SetCustomerID_AP = 20; //--- 2018/03/23

        //ESL
        public static int msg_ReadEslVersion = 16;
        public static int msg_ReadEslBattery = 17;
        public static int msg_ReadManufactureData = 18;
        public static int msg_WriteManufactureData = 19;



        public static int msg_SetCustomerID_ESL = 21;   //--- 2018/03/23
        public static int msg_ReadEslType = 22;        //--- 2018/03/23
        public static int msg_WriteEslData2 = 23;       //--- 2018/03/23
        public static int msg_WriteEslDataFinish2 = 24; //--- 2018/03/23

        // -----  EventHandler ---
        public event EventHandler onSMCEslReceiveEvent;
        private event EventHandler<EslEventArgs> UdpEvent;
        private delegate void BleMacMethodInvoker(byte[] data); //收資料

        //-----------
        private bool isTransmission = false;
        private string WriteMacAddress;
        private int ESL_Index = 0; // esl位置
        private int ESL_DataIndex = 0; //esl資料

        private string TotalBlockData = "";

        private string BlackData = "";  //儲存轉化好的
        private string RedData = ""; //儲存轉化好的
        private string TotalBlackData = ""; //編譯好的
        private string TotalRedData = ""; //編譯好的

        private string cid;

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        private class EslEventArgs : EventArgs
        {
            public byte[] data;
            public string deviceIP;
        }
        public class SMCEslReceiveEventArgs : EventArgs //SMCEslReceiveEventArgs
        {
            public int msgId;
            public bool status = false;
            public string apIP = "";
            public string data = "";
            public double battery;
        }

        //---------------------------------------------------------------------------------
        //Beacon  uel
        public static string URL_Httpw = "00"; //http://wwww
        public static string URL_Httpsw = "01"; //https://wwww
        public static string URL_Http = "02"; //http://
        public static string URL_Https = "03"; //https://
        //-------------------------------------------------------------------------------------

        private Socket TcpClient;


        ~SmcEsl()
        {
            //receiver.Close();
            //this.TcpClient.Close();
        }


        public SmcEsl(Socket client)
        {
            this.TcpClient = client;
            UdpEvent = new EventHandler<EslEventArgs>(BleMacListUpdateUI); //委派自己
        }

        private void Receive(Socket client)
        {
            // try
            // {
            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = client;

            // Begin receiving the data from the remote device.
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            /*   }
               catch (Exception e)
               {
                   Console.WriteLine(e.ToString());
               }*/
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);
                byte[] temp = new byte[bytesRead];
                if (bytesRead > 0)
                {
                    Array.Copy(state.buffer, 0, temp, 0, bytesRead);
                    state.sb.Append(Tools.ByteArrayToString(temp));
                    try
                    {
                        EslEventArgs obj = new EslEventArgs();
                        obj.data = Tools.StringToByteArray(state.sb.ToString());
                        obj.deviceIP = client.RemoteEndPoint.ToString();
                        UdpEvent(this, obj);
                    }
                    catch (Exception e)
                    {
                        Console.Write(e.ToString() + Environment.NewLine);
                    }
                    state.sb.Clear();

                    // Get the rest of the data.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    if (state.sb.Length > 1)
                    {
                        //Console.WriteLine("收 = "+ state.sb.ToString());
                    }
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, byte[] data)
        {
            client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                int bytesSent = client.EndSend(ar);
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /**
         *  掃描Ble裝置
         *  time = 掃描時間
         */
        // public void startScanBleDevice(int time)
        public void startScanBleDevice()
        {
            // string st = Tools.IntToHex(time, 4);
            //  st = "1001" + st.Substring(2, 2) + st.Substring(0, 2);
            string st = "1001" + "ffff";
            byte[] data = Tools.StringToByteArray(st);
            BleMacUdpclient(data);
        }

        /**
         *  停止掃描Ble裝置
         *  time = 掃描時間
         */
        public void stopScanBleDevice()
        {
            byte[] data = new byte[] { 0x10, 0x00, 0x00, 0x00 }; // stop
            BleMacUdpclient(data);
        }

        /**
         *  Ble裝置連線
         *  
         */
        public void ConnectBleDevice(string address)
        {
            this.WriteMacAddress = address;
            byte[] data = Tools.StringToByteArray("12" + address);
            BleMacUdpclient(data);
        }

        /**
         *  Ble裝置斷線
         *  
         */
        public void DisConnectBleDevice()
        {
            byte[] data = new byte[] { 0x14 }; // 斷線
            BleMacUdpclient(data);
        }

        /**
         *  取得Ble裝置名稱
         *  
         */
        public void ReadBleDeviceName()
        {
            byte[] data = new byte[] { 0x13, 0x01, 0x01 };
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
         *  寫入Ble裝置名稱
         *  
         */
        public void WriteBleDeviceName(string deviceName)
        {
            string nameHex = Tools.ConvertStringToHex(deviceName);
            if (nameHex.Length > 24)
            {
                Console.WriteLine("名稱超過12 Bytes");
                return;
            }
            nameHex = "1381" + nameHex;
            byte[] data = Tools.StringToByteArray(nameHex);
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
         *  設定ESL換頁時間
         *  
         */
        public void setEslTurnPageTime(int time)
        {
            string esltime = Tools.IntToHex(time, 4);
            esltime = "1382" + esltime.Substring(2, 2) + esltime.Substring(0, 2);
            byte[] data = Tools.StringToByteArray(esltime);
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
         *  ESL寫入資料
         *  
         */
        public void WriteESLDataWithBle()
        {
            EnCode(this.WriteMacAddress);
            ESL_Index = 0;
            ESL_DataIndex = 0;
            //  TotalBlackData = TotalBlockData + TotalRedData;
            isTransmission = true;
            WriteEslData();
        }

        /**
         *  ESL寫入資料
         *  
         */
        int eid = 0;
        public void WriteBeaconData(string namespaceID, string seid, Boolean end)
        {
            string name = namespaceID;
            name = Tools.ConvertStringToHex(name);
            string EID = seid;
            if (seid.Length > 12)
            {
                EID = seid.Substring(0, 12);
            }

            for (int i = EID.Length; i < 12; i++)
            {
                EID = "0" + EID;
            }
            for (int i = name.Length; i < 20; i++)
            {
                name = "0" + name;
            }

            string aaa = Tools.IntToHex(eid, 2);
            if (aaa.Length < 2)
            {
                aaa = "0" + aaa;
            }
            string urldata = "16AAFE" + "0000" + name + EID + "0000";


            //url
            /* string smcurl = Tools.ConvertStringToHex("smartchip.com.tw");
             for (int i = smcurl.Length; i < 34; i++)
             {
                 smcurl =  smcurl+ "0";
             }
             urldata = "16AAFE" + "1000"+"00" + smcurl;*/
            string sdata = "";
            if (end)
            {
                sdata = "15" + aaa + "0201060303AAFE" + Tools.IntToHex((urldata.Length / 2), 2) + urldata + "FF";
                eid = 0;
            }
            else
            {
                sdata = "15" + aaa + "0201060303AAFE" + Tools.IntToHex((urldata.Length / 2), 2) + urldata + "00";
                eid++;
                if (eid == 127)
                {
                    eid = 0;
                }
            }

            byte[] data = Tools.StringToByteArray(sdata);
            BleMacUdpclient(data);
        }

        /**
       *  寫入Ble裝置IP
       *  
       */
        private void SetIPAddress(string IPAddress, string MACAddress, string Port)
        {
            string[] stringSeparators = new string[] { "." };
            string[] result;
            result = IPAddress.Split(stringSeparators, StringSplitOptions.None);
            string IP1 = "0";
            string IP2 = "0";
            string IP3 = "0";
            string IP4 = "0";

            int i = 0;
            foreach (string s in result)
            {
                if (i == 0)
                    IP1 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 1)
                    IP2 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 2)
                    IP3 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else
                    IP4 = Tools.IntToHex(Convert.ToInt32(s), 2);
                i++;
            }

            stringSeparators = new string[] { ":" };
            result = MACAddress.Split(stringSeparators, StringSplitOptions.None);

            string MAC1 = "0";
            string MAC2 = "0";
            string MAC3 = "0";
            string MAC4 = "0";
            string MAC5 = "0";
            string MAC6 = "0";

            i = 0;
            foreach (string s in result)
            {
                if (i == 0)
                    MAC1 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 1)
                    MAC2 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 2)
                    MAC3 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 3)
                    MAC4 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 4)
                    MAC5 = Tools.IntToHex(Convert.ToInt32(s), 2);
                else if (i == 5)
                    MAC6 = Tools.IntToHex(Convert.ToInt32(s), 2);
                i++;
            }

            string IP = IP1 + IP2 + IP3 + IP4;
            string MAC = MAC1 + MAC2 + MAC3 + MAC4 + MAC5 + MAC6;
            string sPort = Tools.IntToHex(Convert.ToInt32(Port), 4);
            // Console.Write(IP+  "      "+ MAC + "     "+ sPort + Environment.NewLine);

            string Hex = "16" + IP + MAC + sPort;

            byte[] data = Tools.StringToByteArray(Hex);
            BleMacUdpclient(data);
        }



        //--------------------------------------2017/12/21-------------------------------------------
        //AP
        /**
        *  ESL寫入資料到MCU Buffer
        *  
        */
        public void writeESLDataBuffer(string Address, int selectbuffer)
        {
            this.EnCode(Address);
            this.ESL_Index = 0;
            this.ESL_DataIndex = 0;
            this.WriteMacAddress = Address;
            this.WriteEslDataToBuffer(Address);
        }



        private void WriteEslDataToBuffer(string mac)
        {
            this.ESL_DataIndex = 0;
            this.ESL_Index = 0;
            this.writeBlock();
        }



        private void writeBlock()
        {

            try
            {
                // Console.WriteLine("TotalBlockData.Length" + TotalBlockData.Length);
                //  Console.WriteLine("TotalBlockData.Length" + this.TotalBlockData);
                byte[] byteArray = Tools.StringToByteArray(this.TotalBlockData);
                byte[] numArray = new byte[256];
                Array.Copy(byteArray, this.ESL_DataIndex, numArray, 0, 256);
                string str = Tools.ByteArrayToString(numArray);

                str = string.Concat("17", Tools.IntToHex(0, 2), Tools.IntToHex(this.ESL_Index, 4), str);

                this.BleWrite(Tools.StringToByteArray(str));
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }


        private void writeRed()
        {
            try
            {
                byte[] byteArray = Tools.StringToByteArray(this.TotalRedData);
                byte[] numArray = new byte[256];
                Array.Copy(byteArray, this.ESL_DataIndex, numArray, 0, 256);
                string str = Tools.ByteArrayToString(numArray);

                str = string.Concat("17", Tools.IntToHex(0, 2), Tools.IntToHex(this.ESL_Index, 4), str);
                this.BleWrite(Tools.StringToByteArray(str));
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        /**
        *  從Buffer中的資料更新電子紙
        *  
        */
        public void UpdataESLDataFromBuffer(string Address, int type, int count, int selectbuffer)
        {
            string Hex = "18" + Address + Tools.IntToHex(type, 2) + Tools.IntToHex(count, 2) + Tools.IntToHex(selectbuffer, 2) + "00";
            Console.WriteLine("Hex:" + Hex);
            byte[] data = Tools.StringToByteArray(Hex);
            BleMacUdpclient(data);
        }


        public void setRTCTime(int year, int month, int day, int week, int hour, int minute, int second)
        {
            string Hex = "19" + Tools.IntToHex(year, 2) + Tools.IntToHex(month, 2) + Tools.IntToHex(day, 2) + Tools.IntToHex(week, 2)
                 + Tools.IntToHex(hour, 2) + Tools.IntToHex(minute, 2) + Tools.IntToHex(second, 2);
            byte[] data = Tools.StringToByteArray(Hex);
            BleMacUdpclient(data);
        }

        public void getRTCTime()
        {
            byte[] data = Tools.StringToByteArray("1A");
            BleMacUdpclient(data);
        }

        public void setBeaconTime(int start_year, int start_month, int start_day, int start_hour, int start_minute,
            int end_year, int end_month, int end_day, int end_hour, int end_minute)
        {
            string Hex = "1B" + Tools.IntToHex(start_year, 2) + Tools.IntToHex(start_month, 2) + Tools.IntToHex(start_day, 2) + Tools.IntToHex(start_hour, 2)
                 + Tools.IntToHex(start_minute, 2) + Tools.IntToHex(end_year, 2) + Tools.IntToHex(end_month, 2) + Tools.IntToHex(end_day, 2) + Tools.IntToHex(end_hour, 2)
                  + Tools.IntToHex(end_minute, 2);
            byte[] data = Tools.StringToByteArray(Hex);
            BleMacUdpclient(data);
        }
        //-----------------------2018/03/23-----------------------------
        /**
        *  設定AP客戶碼
        *  
        */
        public void setCustomerID_AP(string id)
        {
            string cid = "1C" + id;
            byte[] data = Tools.StringToByteArray(cid);
            data = BCC(data);
            BleMacUdpclient(data);
        }


        //--------------------------------------2017/12/21-----------------------------
        //ESL
        /**
         *  取得ESL 版本
         *  
         */
        public void ReadEslVersion()
        {
            string datas = "1302";
            byte[] data = Tools.StringToByteArray(datas);
            data = BCC(data);
            BleMacUdpclient(data);
        }
        /**
        *  取得ESL 電量
        *  
        */
        public void ReadEslBattery()
        {
            string datas = "1303";
            byte[] data = Tools.StringToByteArray(datas);
            data = BCC(data);
            BleMacUdpclient(data);
        }
        /**
        *  讀取製造資料
        *  
        */
        public void ReadManufactureData()
        {
            string datas = "1304";
            byte[] data = Tools.StringToByteArray(datas);
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
       *  寫入製造資料
       *  
       */
        public void WriteManufactureData(string Mdata)
        {
            string nameHex = Tools.ConvertStringToHex(Mdata);
            if (nameHex.Length > 24)
            {
                // MessageBox.Show("名稱超過8 Bytes");
                Console.WriteLine("名稱超過12 Bytes" + "");
                return;
            }

            for (int i = nameHex.Length; i < 16; i++)
            {
                nameHex = nameHex + "0";
            }

            string datas = "1384" + nameHex;
            byte[] data = Tools.StringToByteArray(datas);
            data = BCC(data);
            BleMacUdpclient(data);
        }
        //--------------------------------------2018/03/22----------------------------------------------
        //ESL
        /**
        *  取得ESL 的尺寸
        *  
        */
        public void ReadEslType()
        {
            string datas = "1305";
            byte[] data = Tools.StringToByteArray(datas);
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
        *  設定ESL客戶碼
        *  
        */
        public void setCustomerID_ESL(string id)
        {
            string cid = "1385" + id;
            byte[] data = Tools.StringToByteArray(cid);
            data = BCC(data);
            BleMacUdpclient(data);
        }

        /**
        *  ESL寫入資料 
        */
        public void WriteESLDataWithBle2(string id)
        {
            EnCode(this.WriteMacAddress);
            ESL_Index = 0;
            ESL_DataIndex = 0;
            isTransmission = true;
            TotalBlackData = TotalBlockData + TotalRedData;
            cid = id;
            WriteEslData2();
        }



        //---------------------------------------------------------------------------------------------------------


        byte[] SendTempDataByteArray = null;
        //==============================================================================
        private void BleMacUdpclient(byte[] data)
        {
            try
            {
                // Console.WriteLine( Tools.ByteArrayToString(data) );
                SendTempDataByteArray = data;
                Send(TcpClient, data);
                Receive(TcpClient);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        private void BleWrite(byte[] data)
        {
            try
            {
                Console.WriteLine(Tools.ByteArrayToString(data));
                Send(TcpClient, data);
                Receive(TcpClient);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        #region Received
        //-------- 接收資料 ------
        private void BleMacListUpdateUI(object sender, EslEventArgs e)
        {
            byte[] data = e.data;
            string deviceIP = e.deviceIP;
            string receivedText = Tools.ByteArrayToString(data);
            //   Console.WriteLine("收 = " + receivedText + "  IP = "+ deviceIP);
            ReceiverUpdateUI(deviceIP, data);
        }




        private void ReceiverUpdateUI(string deviceIP, byte[] data)
        {
            string receivedText = Tools.ByteArrayToString(data);
            string sizeText = "";
             Console.WriteLine("收 = " + receivedText );

            //Scan
            if (data[0] == 0x11)
            {
                /*  byte[] temp = new byte[11];
                  Array.Copy(data, 0, temp, 0, 11);
                  receivedText = Tools.ByteArrayToString(temp);
                  Console.WriteLine("jjjjj"+ receivedText);
                  receivedText = receivedText.Substring(2, 14) + receivedText.Substring(20, 2);*/
                byte[] size = new byte[1];
                receivedText = Tools.ByteArrayToString(data);
                Console.WriteLine("jjjjj" + receivedText);

                Array.Copy(data, 8, size, 0, 1);
                sizeText = Tools.ByteArrayToString(size);

                byte[] esldata = new byte[10];
                Array.Copy(data, 0, esldata, 0, 10);

                receivedText = Tools.ByteArrayToString(esldata);
                receivedText = receivedText.Substring(2, 14);
                Console.WriteLine("receivedText:" + receivedText + "sizeText:" + sizeText);

                /*     if ((int)data.Length % 11 == 0)
                     {
                         Console.WriteLine("kkkkkkkkk");
                         byte[] numArray = new byte[11];
                         Array.Copy(data, 0, numArray, 0, 11);
                         receivedText = Tools.ByteArrayToString(numArray);
                         receivedText = string.Concat(receivedText.Substring(2, 14), receivedText.Substring(20, 2));
                     }
                     else if ((int)data.Length % 10 == 0)
                     {
                         Console.WriteLine("PPPPPPPPPPPP");
                         byte[] numArray1 = new byte[8];
                         Array.Copy(data, 0, numArray1, 0, 8);
                         receivedText = Tools.ByteArrayToString(numArray1);
                         receivedText = receivedText.Substring(2, receivedText.Length - 2);
                     }*/


                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_ScanDevice;
                obj.data = receivedText + sizeText;
                obj.apIP = deviceIP;

                // byte[] battery_array = new byte[] { data[9], data[8] };
                // String batteryS = Tools.ByteArrayToString(battery_array);
                // double battery = (double)(Tools.ConvertHexToInt(batteryS) * 430 / 100) / 1000;

                // obj.battery = Math.Round(battery, 2);
                string str1 = Tools.ByteArrayToString(new byte[] { data[9], data[8] });
                double num = (double)(Tools.ConvertHexToInt(str1) * 430 / 100) / 1000;
                obj.battery = Math.Round(num, 2);

                onSMCEslReceiveEvent(this, obj);
            }

            //Connect
            else if (data[0] == 0x12 && data[1] == 0x00) //連線成功
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_ConnectEslDevice;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == 0x12 && data[1] == 0xff) //連線失敗
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_ConnectEslDevice;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }

            //DisConnect
            else if (data[0] == 0x14 && data[1] == 0x00) //斷線成功
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_DisconnectEslDevice;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == 0x14 && data[1] == 0xff) //斷線失敗
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_DisconnectEslDevice;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            //Beacon
            else if (data[0] == 0x15 && data[2] == 0x00) //Beacon 設置OK
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_WriteBeacon;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == 0x15 && data[2] == 0xff) //Beacon 設置Fail
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_WriteBeacon;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }

            //Set ESL Data Buffer
            else if (data[0] == 0x17 && data[4] == 0x00) // 成功
            {

                int length = this.BlackData.Length / 512;
                this.ESL_Index++;
                this.ESL_DataIndex += 256;
                if (this.ESL_Index < length)
                {
                    this.writeBlock();
                }
                else if (this.ESL_Index == length)
                {
                    this.ESL_DataIndex = 0;
                    this.writeRed();
                }
                else if ((this.ESL_Index <= length ? false : this.ESL_Index < length * 2))
                {
                    this.writeRed();
                }
                else if (this.ESL_Index == length * 2)
                {
                    SmcEsl.SMCEslReceiveEventArgs sMCEslReceiveEventArg7 = new SmcEsl.SMCEslReceiveEventArgs()
                    {
                        msgId = SmcEsl.msg_WriteESLDataBuffer,
                        status = true,
                        apIP = deviceIP
                    };
                    this.onSMCEslReceiveEvent(this, sMCEslReceiveEventArg7);
                }

                /*   int lan = TotalBlackData.Length / 512;
                   Console.WriteLine("TotalBlackData.Length:" + TotalBlackData.Length+ "lan.Length:" + lan + "ESL_Index:" + ESL_Index);
                   ESL_Index++;
                   ESL_DataIndex += 256;
                   if (ESL_Index >= lan)
                   {
                       SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                       obj.msgId = msg_WriteESLDataBuffer;
                       obj.status = true;
                       obj.apIP = deviceIP;
                       onSMCEslReceiveEvent(this, obj);
                   }
                   else
                   {
                       writeBufferData();
                   }*/
            }
            else if (data[0] == 0x17 && data[4] == 0xff) // 失敗
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_WriteESLDataBuffer;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            //Update ESL
            else if (data[0] == 0x18 && data[1] == 0x00) //Update ESL OK
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_UpdataESLDataFromBuffer;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == 0x18 && data[1] == 0xff) //Update ESL Fail
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_UpdataESLDataFromBuffer;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            // Set RTC time
            else if (data[0] == 0x19 && data[1] == 0x00) // Set RTC time OK
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetRTCTime;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == 0x19 && data[1] == 0xff) // Set RTC time Fail
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetRTCTime;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            // get RTC
            else if (data[0] == (byte)0x1A) // get RTC
            {
                receivedText = receivedText.Substring(2, receivedText.Length - 2);
                byte[] temp = Tools.StringToByteArray(receivedText);
                receivedText = "";
                for (int i = 0; i < temp.Length; i++)
                {
                    string date = Tools.ConvertHexToInt(string.Format("{0:x2}", temp[i])) + "";
                    if (date.Length == 1) date = "0" + date;
                    receivedText += date;
                }


                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_GetRTCTime;
                obj.apIP = deviceIP;
                obj.status = true;
                obj.data = receivedText;
                //   Console.Write("get RTC = " + receivedText + Environment.NewLine);
                onSMCEslReceiveEvent(this, obj);
            }
            // Set Beacon time
            else if (data[0] == (byte)0x1B && data[1] == 0x00) // Set Beacon time OK
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetBeaconTime;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == (byte)0x1B && data[1] == 0xff) // Set Beacon time Fail
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetBeaconTime;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }

            //------------ 2018/03/23 ------------------------

            // Set Customer ID
            else if (data[0] == (byte)0x1C && data[1] == 0x00) // Set Customer ID OK
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetCustomerID_AP;
                obj.status = true;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }
            else if (data[0] == (byte)0x1B && data[1] == 0xff) // Set Customer ID Fail
            {
                SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                obj.msgId = msg_SetCustomerID_AP;
                obj.status = false;
                obj.apIP = deviceIP;
                onSMCEslReceiveEvent(this, obj);
            }


            //====   ESL    =======
            else if (data[0] == 0x13)
            {
                // read name
                if (data[2] == 0x01) // Read Device Name
                {
                    string aa = receivedText.Substring(4, 24);
                    byte[] temp = Tools.StringToByteArray(aa);
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslName;
                    obj.data = Tools.ConvertHexToString(temp);
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                // ESL name
                else if (data[2] == 0x81 && data[3] == 0x00)//DeviceName更新成功
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_WriteEslName;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x81 && data[3] == 0xff) //DeviceName更新失敗
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_WriteEslName;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }


                //esl Data
                else if (data[2] == 0x83 && data[3] == 0x00 && isTransmission == true) //ESL 資料寫入成功
                {
                    ESL_Index++;
                    ESL_DataIndex += 128;
                    Console.WriteLine("ESL_Index" + ESL_Index);
                    //if (NfcIndex == 22 || NfcIndex == 44 || NfcIndex == 66)
                    if (ESL_Index == 22)
                    {
                        ESL_DataIndex = 0;
                    }
                    // if (NfcIndex == 88)
                    if (ESL_Index >= 44)
                    {

                        SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                        obj.msgId = msg_WriteEslDataFinish;
                        obj.apIP = deviceIP;
                        onSMCEslReceiveEvent(this, obj);
                        isTransmission = false;
                    }
                    else
                    {
                        WriteEslData();
                        SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                        obj.msgId = msg_WriteEslData;
                        obj.status = true;
                        obj.apIP = deviceIP;
                        onSMCEslReceiveEvent(this, obj);
                    }
                }
                else if (data[2] == 0x83 && data[3] == 0xff && isTransmission == true) //ESL 資料寫入失敗重送
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_WriteEslData;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }

                //-------------------------------------

                // Read Version
                else if (data[2] == 0x02 && data[data.Length - 1] == R_BCC(data))//Read Version 成功
                {
                    receivedText = receivedText.Substring(2, receivedText.Length - 4);
                    byte[] temp = Tools.StringToByteArray(receivedText);
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslVersion;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    obj.data = Tools.ConvertHexToString(temp);
                    onSMCEslReceiveEvent(this, obj);

                }
                else if (data[2] == 0x02 && data[data.Length - 1] != R_BCC(data))
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslVersion;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }



                // Read Battery
                else if (data[2] == 0x03 && data[data.Length - 1] == R_BCC(data))//Read Battery 成功
                {
                    // AB02 * 440 / 100 = 3.07
                    //430

                    //LSB  MSB
                    byte[] battery_array = new byte[] { data[4], data[3] };
                    String batteryS = Tools.ByteArrayToString(battery_array);
                    double battery = (double)(Tools.ConvertHexToInt(batteryS) * 430 / 100) / 1000;

                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslBattery;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    obj.data = Math.Round(battery, 2) + "";
                    obj.battery = Math.Round(battery, 2);
                    onSMCEslReceiveEvent(this, obj);

                    // Console.WriteLine("Battery = " + Math.Round(battery, 2));
                }
                else if (data[2] == 0x03 && data[data.Length - 1] != R_BCC(data))
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslBattery;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                    //Error
                }

                // Read Manufacture Date
                else if (data[2] == 0x04 && data[data.Length - 1] == R_BCC(data))//Read Manufacture Date 成功
                {
                    receivedText = receivedText.Substring(2, receivedText.Length - 4);
                    byte[] temp = Tools.StringToByteArray(receivedText);
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadManufactureData;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    obj.data = Tools.ConvertHexToString(temp);
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x04 && data[data.Length] != R_BCC(data))
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadManufactureData;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                    //Error
                }

                // Write Manufacture Date
                else if (data[2] == 0x84 && data[3] == 0x00)//Write Manufacture Date 成功
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_WriteManufactureData;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x84 && data[3] == 0xff) //Write Manufacture Date 失敗
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_WriteManufactureData;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                //------------ 2018/03/23 ------------------------
                else if (data[2] == 0x05 && data[data.Length - 1] == R_BCC(data))
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslType;
                    obj.data = receivedText;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x05 && data[data.Length - 1] != R_BCC(data))
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_ReadEslType;
                    obj.data = receivedText;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x85 && data[3] == 0x00)//set Customer ID 成功
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_SetCustomerID_ESL;
                    obj.status = true;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }
                else if (data[2] == 0x85 && data[3] == 0xff) //set Customer ID 失敗
                {
                    SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                    obj.msgId = msg_SetCustomerID_ESL;
                    obj.status = false;
                    obj.apIP = deviceIP;
                    onSMCEslReceiveEvent(this, obj);
                }

                //esl Data
                // else if (data[2] == 0x86 && data[3] == 0x00 && isTransmission == true) //ESL 資料寫入成功
                else if (data[2] == 0x86 && isTransmission == true) //ESL 資料寫入成功
                {
                    int lan = TotalBlackData.Length / 256;
                    ESL_Index++;
                    ESL_DataIndex += 128;
                    Console.WriteLine("ESL_Index" + ESL_Index + "lan:" + lan);
                    if (ESL_Index >= lan)
                    {

                        SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                        obj.msgId = msg_WriteEslDataFinish2;
                        obj.apIP = deviceIP;
                        onSMCEslReceiveEvent(this, obj);
                        isTransmission = false;
                    }
                    else
                    {
                        WriteEslData2();
                        SMCEslReceiveEventArgs obj = new SMCEslReceiveEventArgs();
                        obj.msgId = msg_WriteEslData2;
                        obj.status = true;
                        obj.apIP = deviceIP;
                        onSMCEslReceiveEvent(this, obj);
                    }
                }




            }
        }



        int selectbufferIndex;
        //-------------2017/12/21----------
        private void WriteEslDataToBuffer(string mac, int selectbuffer)
        {
            ESL_DataIndex = 0;
            ESL_Index = 0;
            selectbufferIndex = selectbuffer;

            // TotalBlackData = TotalBlockData + TotalRedData;
            writeBufferData();
        }
        // Buffer Black
        private void writeBufferData()
        {
            try
            {
                byte[] blockdata = Tools.StringToByteArray(TotalBlackData);
                byte[] temp = new byte[256];
                Array.Copy(blockdata, ESL_DataIndex, temp, 0, 256);
                Console.WriteLine("temp" + temp);
                string stemp = Tools.ByteArrayToString(temp);

                stemp = "17" + Tools.IntToHex(selectbufferIndex, 2) + Tools.IntToHex(ESL_Index, 4) + stemp;
                Console.WriteLine("stemp" + stemp);
                byte[] data = Tools.StringToByteArray(stemp);
                BleWrite(data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        //加密混淆
        private void EnCode(string mac)
        {
            /* TotalBlackData = "";
             TotalRedData = "";
             try
             {
                 byte[] blockdata = Tools.StringToByteArray(BlackData);
                 int lan = BlackData.Length / 256;
                 byte[] addarray = Tools.StringToByteArray(mac.Substring(0, 6));
                 int index = 0;
                 for (int j = 0; j < lan; j++)
                 {
                     byte[] temp = new byte[128];
                     Array.Copy(blockdata, index, temp, 0, 128);
                     byte sum = (byte)0x00;
                     for (int i = 0; i < 3; i++)
                     {
                         sum = (byte)(sum + addarray[i]);
                     }
                     for (int i = 0; i < 128; i++)
                     {
                         temp[i] = (byte)((sum) ^ temp[i]);
                         sum = (byte)(sum + 0x02);
                     }
                     index += 128;
                     TotalBlackData += Tools.ByteArrayToString(temp);
                 }
                 byte[] reddata = Tools.StringToByteArray(RedData);
                 index = 0;
                 for (int j = 0; j < lan; j++)
                 {
                     byte[] temp = new byte[128];
                     Array.Copy(reddata, index, temp, 0, 128);

                     byte sum = (byte)0x00;
                     for (int i = 0; i < 3; i++)
                     {
                         sum = (byte)(sum + addarray[i]);
                     }
                     for (int i = 0; i < 128; i++)
                     {
                         temp[i] = (byte)((sum) ^ temp[i]);
                         sum = (byte)(sum + 0x03);
                     }
                     index += 128;
                     TotalRedData += Tools.ByteArrayToString(temp);
                 }
             }
             catch (Exception e)
             {
                 Console.WriteLine("Error:" + e.ToString());
             }*/

            this.TotalBlockData = "";
            this.TotalRedData = "";
            try
            {
                byte[] byteArray = Tools.StringToByteArray(this.BlackData);
                int length = this.BlackData.Length / 256;
                byte[] numArray = Tools.StringToByteArray(mac.Substring(0, 6));
                int num = 0;
                for (int i = 0; i < length; i++)
                {
                    byte[] numArray1 = new byte[128];
                    Array.Copy(byteArray, num, numArray1, 0, 128);
                    byte num1 = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        num1 = (byte)(num1 + numArray[j]);
                    }
                    for (int k = 0; k < 128; k++)
                    {
                        numArray1[k] = (byte)(num1 ^ numArray1[k]);
                        num1 = (byte)(num1 + 2);
                    }
                    num += 128;
                    this.TotalBlockData = string.Concat(this.TotalBlockData, Tools.ByteArrayToString(numArray1));
                }
                byte[] byteArray1 = Tools.StringToByteArray(this.RedData);
                num = 0;
                for (int l = 0; l < length; l++)
                {
                    byte[] numArray2 = new byte[128];
                    Array.Copy(byteArray1, num, numArray2, 0, 128);
                    byte num2 = 0;
                    for (int m = 0; m < 3; m++)
                    {
                        num2 = (byte)(num2 + numArray[m]);
                    }
                    for (int n = 0; n < 128; n++)
                    {
                        numArray2[n] = (byte)(num2 ^ numArray2[n]);
                        num2 = (byte)(num2 + 3);
                    }
                    num += 128;
                    this.TotalRedData = string.Concat(this.TotalRedData, Tools.ByteArrayToString(numArray2));
                }

            }
            catch (Exception exception)
            {
                Console.WriteLine(string.Concat("Error:", exception.ToString()));
            }
        }


        //---------------------------------
        //直接寫入ESL，非寫入buffer
        private void WriteEslData()
        {

            if (ESL_Index < 22)
            {
                try
                {
                    byte[] blockdata = Tools.StringToByteArray(TotalBlockData);
                    Console.WriteLine("blockdata.Length" + blockdata.Length);
                    byte[] temp = new byte[128];
                    Array.Copy(blockdata, ESL_DataIndex, temp, 0, 128);
                    string stemp = Tools.ByteArrayToString(temp);
                    stemp = "1383" + Tools.IntToHex(ESL_Index, 2) + stemp;
                    byte[] data = Tools.StringToByteArray(stemp);
                    data = BCC(data);
                    BleWrite(data);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.ToString());
                }
            }
            else if (ESL_Index < 44)
            {
                try
                {
                    byte[] blockdata = Tools.StringToByteArray(TotalRedData);
                    byte[] temp = new byte[128];
                    Array.Copy(blockdata, ESL_DataIndex, temp, 0, 128);
                    string stemp = Tools.ByteArrayToString(temp);
                    stemp = "1383" + Tools.IntToHex(ESL_Index, 2) + stemp;
                    byte[] data = Tools.StringToByteArray(stemp);
                    data = BCC(data);
                    BleWrite(data);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.ToString());
                }
            }
        }

        /** --------------------------------------------------------------------------------
         * 2018/03/22
         * 增加指令
         */
        private void WriteEslData2() //直接寫入
        {
            try
            {
                byte[] blockdata = Tools.StringToByteArray(TotalBlackData);
                byte[] temp = new byte[128];
                Array.Copy(blockdata, ESL_DataIndex, temp, 0, 128);
                string stemp = Tools.ByteArrayToString(temp);
                string indexhex = ESL_Index.ToString("X4");
                stemp = "1386" + cid + indexhex.Substring(2, 2) + indexhex.Substring(0, 2) + stemp;

                Console.WriteLine("Send = " + stemp);

                byte[] data = Tools.StringToByteArray(stemp);
                data = BCC(data);
                BleWrite(data);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error:" + e.ToString());
            }
        }


        //---------------


        //直接載入HEX
        public void setHexData(string black, string red)
        {
            BlackData = black;
            RedData = red;
            int flen = 256 - (BlackData.Length % 256) + BlackData.Length;
            Console.WriteLine("BlackData.Length" + BlackData.Length);
            for (int i = BlackData.Length; i < flen; i++)//2768
            {
                BlackData = BlackData + "0";
                RedData = RedData + "0";
            }
        }

        //圖面全黑
        public void setBlackData()
        {
            string Black = "";
            string Red = "";
            int flen = 256 - (BlackData.Length % 256) + BlackData.Length;
            for (int i = Black.Length; i < flen; i++)
            {
                Black += "0";
                Red += "0";
            }
            BlackData = Black;
            RedData = Red;
        }
        //圖面全白
        public void setWhileData()
        {
            string Black = "";
            string Red = "";
            int flen = 256 - (BlackData.Length % 256) + BlackData.Length;
            for (int i = Black.Length; i < flen; i++)
            {
                Black += "F";
                Red += "0";
            }
            BlackData = Black;
            RedData = Red;
        }
        //圖面全紅
        public void setRedData()
        {
            string Black = "";
            string Red = "";
            int flen = 256 - (BlackData.Length % 256) + BlackData.Length;
            for (int i = Black.Length; i < flen; i++)
            {
                Black += "F";
                Red += "F";
            }
            BlackData = Black;
            RedData = Red;
        }

        //圖像轉HEX
        /*  public void TransformImageToData(Bitmap bmp)
          {

              if (bmp != null)
              {
                  string str = "";
                  string str1 = "";
                  string str2 = "";
                  string str3 = "";
                 // Console.WriteLine("bmp.Width:" + bmp.Width);
                //  Console.WriteLine("bmp.Height:" + bmp.Height);
                  for (int i = bmp.Width - 1; i >= 0; i--)
                  {
                      for (int j = 0; j < bmp.Height; j++)
                      {
                          Color pixel = bmp.GetPixel(i, j);

                       //   str = (pixel.ToArgb() != Color.Black.ToArgb() ? string.Concat(str, "1") : string.Concat(str, "0"));
                       //   str1 = (pixel.ToArgb() != Color.Red.ToArgb() ? string.Concat(str1, "0") : string.Concat(str1, "1"));
                          str = string.Concat(str, "1");
                          str1 =string.Concat(str1, "1");
                          if (str.Length == 8)
                          {
                              int num = Convert.ToInt32(str, 2);
                              str2 = string.Concat(str2, num.ToString("X2"));
                              num = Convert.ToInt32(str1, 2);
                              str3 = string.Concat(str3, num.ToString("X2"));


                              str = "";
                              str1 = "";
                          }
                      }
                  }




                  this.BlackData = str2;
                  this.RedData = str3;



                  int length = 256 - this.BlackData.Length % 256 + this.BlackData.Length;
                  Console.WriteLine("this.BlackData.Length % 256 :" + this.BlackData.Length % 256);
                  Console.WriteLine("this.BlackData.Length:" + this.BlackData.Length);
                  Console.WriteLine("length:" + length);
                  for (int k = this.BlackData.Length; k < length; k++)
                  {
                      this.BlackData = string.Concat(this.BlackData, "0");
                      this.RedData = string.Concat(this.RedData, "0");
                  }
              }
          }

   */

        public void TransformImageToData(Bitmap bmp)
        {
            Color pixel;
            int num;
            if (bmp != null)
            {
                string str = "";
                string str1 = "";
                string str2 = "";
                string str3 = "";
                if (bmp.Width <= 380)
                {
                    for (int i = bmp.Width - 1; i >= 0; i--)
                    {
                        for (int j = 0; j < bmp.Height; j++)
                        {
                            pixel = bmp.GetPixel(i, j);
                            str = (pixel.ToArgb() != Color.Black.ToArgb() ? string.Concat(str, "1") : string.Concat(str, "0"));
                            str1 = (pixel.ToArgb() != Color.Red.ToArgb() ? string.Concat(str1, "0") : string.Concat(str1, "1"));
                            if (str.Length == 8)
                            {
                                num = Convert.ToInt32(str, 2);
                                str2 = string.Concat(str2, num.ToString("X2"));
                                num = Convert.ToInt32(str1, 2);
                                str3 = string.Concat(str3, num.ToString("X2"));
                                str = "";
                                str1 = "";
                            }
                        }
                    }
                }
                else
                {
                    for (int k = 0; k < bmp.Height; k++)
                    {
                        for (int l = 0; l < bmp.Width; l++)
                        {
                            pixel = bmp.GetPixel(l, k);
                            str = (pixel.ToArgb() != Color.Black.ToArgb() ? string.Concat(str, "1") : string.Concat(str, "0"));
                            str1 = (pixel.ToArgb() != Color.Red.ToArgb() ? string.Concat(str1, "0") : string.Concat(str1, "1"));
                            if (str.Length == 8)
                            {
                                num = Convert.ToInt32(str, 2);
                                str2 = string.Concat(str2, num.ToString("X2"));
                                num = Convert.ToInt32(str1, 2);
                                str3 = string.Concat(str3, num.ToString("X2"));
                                str = "";
                                str1 = "";
                            }
                        }
                    }
                }
                this.BlackData = str2;
                this.RedData = str3;
                if (this.BlackData.Length % 256 != 0)
                {
                    int length = 256 - this.BlackData.Length % 256 + this.BlackData.Length;
                    for (int m = this.BlackData.Length; m < length; m++)
                    {
                        this.BlackData = string.Concat(this.BlackData, "0");
                        this.RedData = string.Concat(this.RedData, "0");
                    }
                }
            }
        }






        #endregion

        private static byte[] BCC(byte[] data)
        {
            byte[] temp = new byte[data.Length + 2];
            byte intBytes = Convert.ToByte(data.Length);

            byte bcc = (byte)0x00;
            int j = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (i == 1)
                {
                    temp[i] = intBytes;
                    j = 1;
                }
                if (j == 1)
                {
                    temp[i + 1] = data[i];
                }
                else
                {
                    temp[i] = data[i];
                }

            }
            for (int i = 1; i < data.Length; i++)
            {
                bcc = (byte)(bcc ^ data[i]);
            }
            temp[data.Length + 1] = bcc;
            return temp;
        }

        private static byte R_BCC(byte[] data)
        {
            byte bcc = (byte)0x00;

            for (int i = 2; i < data.Length - 1; i++)
            {
                bcc = (byte)(bcc ^ data[i]);
            }
            return bcc;
        }
    }

}