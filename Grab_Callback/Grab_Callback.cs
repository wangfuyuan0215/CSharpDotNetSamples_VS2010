using System;
using System.Collections.Generic;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;
using System.Runtime.InteropServices;
using System.IO;

namespace Grab_Callback
{

    class Grab_Callback
    {
        private static cbOutputExdelegate ImageCallback;

        static void ImageCallbackFunc(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
        {
            Console.WriteLine("Get one frame: Width[" + Convert.ToString(pFrameInfo.nWidth) + "] , Height[" + Convert.ToString(pFrameInfo.nHeight)
                                + "] , FrameNum[" + Convert.ToString(pFrameInfo.nFrameNum) + "]");
        }

        static void Main(string[] args)
        {
            int nRet = CErrorDefine.MV_OK;
            bool m_bIsDeviceOpen = false;       // ch:设备打开状态 | en:Is device open
            CCamera m_MyCamera = new CCamera();

            do
            {
                List<CCameraInfo> ltDeviceList = new List<CCameraInfo>();

                // ch:枚举设备 | en:Enum device
                nRet = CSystem.EnumDevices(CSystem.MV_GIGE_DEVICE | CSystem.MV_USB_DEVICE, ref ltDeviceList);
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Enum device failed:{0:x8}", nRet);
                    break;
                }
                Console.WriteLine("Enum device count : " + Convert.ToString(ltDeviceList.Count));
                if (0 == ltDeviceList.Count)
                {
                    break;
                }

                // ch:打印设备信息 en:Print device info
                for (int i = 0; i < ltDeviceList.Count; i++)
                {
                    if (CSystem.MV_GIGE_DEVICE == ltDeviceList[i].nTLayerType)
                    {
                        CGigECameraInfo cGigEDeviceInfo = (CGigECameraInfo)ltDeviceList[i];

                        uint nIp1 = ((cGigEDeviceInfo.nCurrentIp & 0xff000000) >> 24);
                        uint nIp2 = ((cGigEDeviceInfo.nCurrentIp & 0x00ff0000) >> 16);
                        uint nIp3 = ((cGigEDeviceInfo.nCurrentIp & 0x0000ff00) >> 8);
                        uint nIp4 = (cGigEDeviceInfo.nCurrentIp & 0x000000ff);

                        Console.WriteLine("[device " + i.ToString() + "]:");
                        Console.WriteLine("  DevIP:" + nIp1 + "." + nIp2 + "." + nIp3 + "." + nIp4);
                        if ("" != cGigEDeviceInfo.UserDefinedName)
                        {
                            Console.WriteLine("  UserDefineName:" + cGigEDeviceInfo.UserDefinedName + "\n");
                        }
                        else
                        {
                            Console.WriteLine("  ManufacturerName:" + cGigEDeviceInfo.chManufacturerName + "\n");
                        }
                    }
                    else if (CSystem.MV_USB_DEVICE == ltDeviceList[i].nTLayerType)
                    {
                        CUSBCameraInfo cUsb3DeviceInfo = (CUSBCameraInfo)ltDeviceList[i];

                        Console.WriteLine("[device " + i.ToString() + "]:");
                        Console.WriteLine("  SerialNumber:" + cUsb3DeviceInfo.chSerialNumber);
                        if ("" != cUsb3DeviceInfo.UserDefinedName)
                        {
                            Console.WriteLine("  UserDefineName:" + cUsb3DeviceInfo.UserDefinedName + "\n");
                        }
                        else
                        {
                            Console.WriteLine("  ManufacturerName:" + cUsb3DeviceInfo.chManufacturerName + "\n");
                        }
                    }
                }

                // ch:选择设备序号 | en:Select device
                int nDevIndex = 0;
                Console.Write("Please input index(0-{0:d}):", ltDeviceList.Count - 1);
                try
                {
                    nDevIndex = Convert.ToInt32(Console.ReadLine());
                }
                catch
                {
                    Console.Write("Invalid Input!\n");
                    break;
                }

                if (nDevIndex > ltDeviceList.Count - 1 || nDevIndex < 0)
                {
                    Console.Write("Input Error!\n");
                    break;
                }

                // ch:获取选择的设备信息 | en:Get selected device information
                CCameraInfo stDevice = ltDeviceList[nDevIndex];

                // ch:创建设备 | en:Create device
                nRet = m_MyCamera.CreateHandle(ref stDevice);
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Create device failed:{0:x8}", nRet);
                    break;
                }

                // ch:打开设备 | en:Open device
                nRet = m_MyCamera.OpenDevice();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Open device failed:{0:x8}", nRet);
                    break;
                }
                m_bIsDeviceOpen = true;

                // ch:探测网络最佳包大小(只对GigE相机有效) | en:Detection network optimal package size(It only works for the GigE camera)
                if (CSystem.MV_GIGE_DEVICE == stDevice.nTLayerType)
                {
                    int nPacketSize = m_MyCamera.GIGE_GetOptimalPacketSize();
                    if (nPacketSize > 0)
                    {
                        nRet = m_MyCamera.SetIntValue("GevSCPSPacketSize", (uint)nPacketSize);
                        if (CErrorDefine.MV_OK != nRet)
                        {
                            Console.WriteLine("Warning: Set Packet Size failed {0:x8}", nRet);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Get Packet Size failed {0:x8}", nPacketSize);
                    }
                }

                // ch:设置触发模式为off || en:set trigger mode as off
                nRet = m_MyCamera.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Set TriggerMode failed:{0:x8}", nRet);
                    break;
                }

                // ch:注册回调函数 | en:Register image callback
                ImageCallback = new cbOutputExdelegate(ImageCallbackFunc);
                nRet = m_MyCamera.RegisterImageCallBackEx(ImageCallback, IntPtr.Zero);
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Register image callback failed!");
                    break;
                }

                // ch:开启抓图 || en: start grab image
                nRet = m_MyCamera.StartGrabbing();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Start grabbing failed:{0:x8}", nRet);
                    break;
                }

                Console.WriteLine("Press enter to exit");
                Console.ReadLine();

                // ch:停止抓图 | en:Stop grabbing
                nRet = m_MyCamera.StopGrabbing();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Stop grabbing failed:{0:x8}", nRet);
                    break;
                }

                // ch:关闭设备 | en:Close device
                nRet = m_MyCamera.CloseDevice();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Close device failed:{0:x8}", nRet);
                    break;
                }
                m_bIsDeviceOpen = false;

                // ch:销毁设备 | en:Destroy device
                nRet = m_MyCamera.DestroyHandle();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Destroy device failed:{0:x8}", nRet);
                    break;
                }
            } while (false);

            if (CErrorDefine.MV_OK != nRet)
            {
                // ch:关闭设备 | en:Close device
                if (true == m_bIsDeviceOpen)
                {
                    nRet = m_MyCamera.CloseDevice();
                    if (CErrorDefine.MV_OK != nRet)
                    {
                        Console.WriteLine("Close device failed:{0:x8}", nRet);
                    }
                }

                // ch:销毁设备 | en:Destroy device
                nRet = m_MyCamera.DestroyHandle();
                if (CErrorDefine.MV_OK != nRet)
                {
                    Console.WriteLine("Destroy device failed:{0:x8}", nRet);
                }
            }

            Console.WriteLine("Press enter to exit");
            Console.ReadKey();
        }
    }
}
