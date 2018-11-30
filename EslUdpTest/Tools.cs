using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace EslUdpTest
{
    public class Tools
    {
        public Tools()
        {
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder stringBuilder = new StringBuilder((int)ba.Length * 2);
            byte[] numArray = ba;
            for (int i = 0; i < (int)numArray.Length; i++)
            {
                stringBuilder.AppendFormat("{0:X2}", numArray[i]);
            }
            return stringBuilder.ToString();
        }

        public static string ConvertBinaryToHex(string strBinary)
        {
            return Convert.ToInt32(strBinary, 2).ToString("x8");
        }

        public static int ConvertHexToInt(string hex)
        {
            return int.Parse(hex, NumberStyles.HexNumber);
        }

        public static string ConvertHexToString(byte[] HexValue)
        {
            return Encoding.UTF8.GetString(HexValue);
        }

        public static string ConvertHexToString(string HexValue)
        {
            string str = "";
            while (HexValue.Length > 0)
            {
                char chr = Convert.ToChar(Convert.ToUInt32(HexValue.Substring(0, 2), 16));
                str = string.Concat(str, chr.ToString());
                HexValue = HexValue.Substring(2, HexValue.Length - 2);
            }
            return str;
        }

        public static string ConvertStringToHex(string text)
        {
            return Tools.ByteArrayToString(Encoding.UTF8.GetBytes(text));
        }

        public static string ConvertCharToHex(string text)
        {
            char[] values = text.ToCharArray();
            string result = "";
            foreach (char letter in values)
            {
                // Get the integral value of the character.
                int value = Convert.ToInt32(letter);
                // Convert the decimal value to a hexadecimal value in string form.
                string hexOutput = String.Format("{0:X}", value);
                result = result + hexOutput;
                Console.WriteLine("Hexadecimal value of {0} is {1}", letter, hexOutput);
            }

            return result;
        }
        
        public static byte[] iCheckSum(byte[] data)
        {
            byte[] numArray = new byte[2];
            int num = 0;
            for (int i = 0; i < (int)data.Length; i++)
            {
                num += data[i];
            }
            byte[] bytes = BitConverter.GetBytes(num);
            Array.Reverse(bytes);
            numArray[0] = bytes[(int)bytes.Length - 2];
            numArray[1] = bytes[(int)bytes.Length - 1];
            return numArray;
        }

        public static string IntToHex(int iValue, int len)
        {
            string str = null;
            if (len == 1)
            {
                str = iValue.ToString("X");
            }
            else if (len == 2)
            {
                str = iValue.ToString("X2");
            }
            else if (len == 3)
            {
                str = iValue.ToString("X3");
            }
            else if (len == 4)
            {
                str = iValue.ToString("X4");
            }
            else if (len == 5)
            {
                str = iValue.ToString("X5");
            }
            else if (len == 6)
            {
                str = iValue.ToString("X6");
            }
            return str;
        }

        public void SNC_GetAP_Info()
        {
            List<Tools.AP_Information> aPInformations = new List<Tools.AP_Information>();
            byte[] numArray = new byte[] { 255, 1, 1, 2 };
            IPEndPoint pEndPoint = new IPEndPoint(IPAddress.Broadcast, 1500);
            NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < (int)allNetworkInterfaces.Length; i++)
            {
                NetworkInterface networkInterface = allNetworkInterfaces[i];
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet && networkInterface.Supports(NetworkInterfaceComponent.IPv4))
                {
                    try
                    {
                        foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                            {
                                continue;
                            }
                            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                            socket.ReceiveTimeout = 200;
                            socket.Bind(new IPEndPoint(unicastAddress.Address, 1500));
                            socket.SendTo(numArray, pEndPoint);
                            byte[] numArray1 = new byte[1024];
                            do
                            {
                                try
                                {
                                    int num = socket.Receive(numArray1);
                                    byte[] numArray2 = new byte[num];
                                    Array.Copy(numArray1, 0, numArray2, 0, num);
                                    if (num == 36)
                                    {
                                        string str = Tools.ByteArrayToString(numArray2);
                                        int num1 = Tools.ConvertHexToInt(str.Substring(10, 2));
                                        int num2 = Tools.ConvertHexToInt(str.Substring(12, 2));
                                        int num3 = Tools.ConvertHexToInt(str.Substring(14, 2));
                                        int num4 = Tools.ConvertHexToInt(str.Substring(16, 2));
                                        string str1 = string.Concat(new object[] { num1, ".", num2, ".", num3, ".", num4 });
                                        string str2 = str.Substring(18, 12);
                                        string str3 = str.Substring(38, 32);
                                        Tools.AP_Information aPInformation = new Tools.AP_Information()
                                        {
                                            AP_IP = str1,
                                            AP_MAC_Address = str2,
                                            AP_Name = Tools.ConvertHexToString(str3)
                                        };
                                        aPInformations.Add(aPInformation);
                                    }
                                }
                                catch (Exception exception)
                                {
                                    break;
                                }
                            }
                            while (socket.ReceiveTimeout != 0);
                            socket.Close();
                        }
                    }
                    catch
                    {
                    }
                }
            }
            Tools.ApScanEventArgs apScanEventArg = new Tools.ApScanEventArgs()
            {
                data = aPInformations
            };
            this.onApScanEvent(this, apScanEventArg);
        }

        public static byte[] StringToByteArray(string hex)
        {
            int length = hex.Length;
            byte[] num = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                num[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return num;
        }

        public event EventHandler onApScanEvent;

        public class AP_Information
        {
            public string AP_IP = "";

            public string AP_MAC_Address = "";

            public string AP_Name = "";

            public AP_Information()
            {
            }
        }

        public class ApScanEventArgs : EventArgs
        {
            public List<Tools.AP_Information> data;

            public ApScanEventArgs()
            {
            }
        }
    }
}