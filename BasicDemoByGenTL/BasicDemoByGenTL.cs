using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace BasicDemoByGenTL
{
    public partial class Form1 : Form
    {
        List<CGenTLIFInfo> m_ltIFInfoList = new List<CGenTLIFInfo>();
        List<CGenTLDevInfo> m_ltDeviceList = new List<CGenTLDevInfo>();
        private CCamera m_MyCamera = new CCamera();
        bool m_bGrabbing = false;
        Thread m_hReceiveThread = null;

        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        // ch:显示错误信息 | en:Show error message
        private void ShowErrorMsg(string csMessage, int nErrorNum)
        {
            string errorMsg;
            if (nErrorNum == 0)
            {
                errorMsg = csMessage;
            }
            else
            {
                errorMsg = csMessage + ": Error =" + String.Format("{0:X}", nErrorNum);
            }

            switch (nErrorNum)
            {
                case CErrorDefine.MV_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case CErrorDefine.MV_E_SUPPORT: errorMsg += " Not supported function "; break;
                case CErrorDefine.MV_E_BUFOVER: errorMsg += " Cache is full "; break;
                case CErrorDefine.MV_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case CErrorDefine.MV_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case CErrorDefine.MV_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case CErrorDefine.MV_E_NODATA: errorMsg += " No data "; break;
                case CErrorDefine.MV_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case CErrorDefine.MV_E_VERSION: errorMsg += " Version mismatches "; break;
                case CErrorDefine.MV_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case CErrorDefine.MV_E_UNKNOW: errorMsg += " Unknown error "; break;
                case CErrorDefine.MV_E_GC_GENERIC: errorMsg += " General error "; break;
                case CErrorDefine.MV_E_GC_ACCESS: errorMsg += " Node accessing condition error "; break;
                case CErrorDefine.MV_E_ACCESS_DENIED: errorMsg += " No permission "; break;
                case CErrorDefine.MV_E_BUSY: errorMsg += " Device is busy, or network disconnected "; break;
                case CErrorDefine.MV_E_NETER: errorMsg += " Network error "; break;
            }

            MessageBox.Show(errorMsg, "PROMPT");
        }

        private void btnEnumInterface_Click(object sender, EventArgs e)
        {
            System.GC.Collect();
            cmbDeviceList.Items.Clear();
            cmbInterfaceList.Items.Clear();
            cmbDeviceList.Text = "";
            cmbInterfaceList.Text = "";
            OpenFileDialog FileDialog = new OpenFileDialog();
            if (null == FileDialog)
            {
                ShowErrorMsg("Open File Dialog Fail!", CErrorDefine.MV_E_RESOURCE);
                return;
            }

            //获取选择的DCF文件路径
            FileDialog.Filter = "DCF文件(*.cti)|*.cti";
            FileDialog.ShowDialog();

            int nRet = CSystem.EnumInterfaceByGenTL(ref m_ltIFInfoList, FileDialog.FileName);
            if (0 != nRet)
            {
                ShowErrorMsg("Enumerate interfaces fail!", nRet);
                return;
            }
            for (Int32 i = 0; i < m_ltIFInfoList.Count; i++ )
            {
                cmbInterfaceList.Items.Add("TLType:" + m_ltIFInfoList[i].chTLType + " " + m_ltIFInfoList[i].chInterfaceID + " " + m_ltIFInfoList[i].chDisplayName);
            }
            cmbInterfaceList.SelectedIndex = 0;

            if (m_ltIFInfoList.Count > 0)
            {
                btnEnumDevice.Enabled = true;
            }
        }

        private void btnEnumDevice_Click(object sender, EventArgs e)
        {
            DeviceListAcq();

            bnOpen.Enabled = true;
        }

        private void DeviceListAcq()
        {
            // ch:创建设备列表 | en:Create Device List
            System.GC.Collect();
            cmbDeviceList.Items.Clear();
            cmbDeviceList.Text = "";

            CGenTLIFInfo pcIFInfo = m_ltIFInfoList[cmbInterfaceList.SelectedIndex];

            int nRet = CSystem.EnumCameraListByGenTL(ref pcIFInfo, ref m_ltDeviceList);
            if (0 != nRet)
            {
                ShowErrorMsg("Enumerate devices fail!", 0);
                return;
            }

            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            for (int i = 0; i < m_ltDeviceList.Count; i++)
            {
                CGenTLDevInfo device = m_ltDeviceList[i];

                if (device.UserDefinedName != "")
                {
                    cmbDeviceList.Items.Add("Dev: " + device.UserDefinedName + " (" + device.chSerialNumber + ")");
                }
                else
                {
                    cmbDeviceList.Items.Add("Dev: " + device.chVendorName + " " + device.chModelName + " (" + device.chSerialNumber + ")");
                }
            }

            // ch:选择第一项 | en:Select the first item
            if (m_ltDeviceList.Count != 0)
            {
                cmbDeviceList.SelectedIndex = 0;
            }
        }

        private void SetCtrlWhenOpen()
        {
            btnEnumInterface.Enabled = false;
            btnEnumDevice.Enabled = false;
            bnOpen.Enabled = false;

            bnClose.Enabled = true;
            bnStartGrab.Enabled = true;
            bnStopGrab.Enabled = false;
            bnContinuesMode.Enabled = true;
            bnContinuesMode.Checked = true;
            bnTriggerMode.Enabled = true;
            cbSoftTrigger.Enabled = false;
            bnTriggerExec.Enabled = false;
        }

        private void bnOpen_Click(object sender, EventArgs e)
        {
            if (m_ltDeviceList.Count == 0 || cmbDeviceList.SelectedIndex == -1)
            {
                ShowErrorMsg("No device, please select", 0);
                return;
            }

            // ch:获取选择的设备信息 | en:Get selected device information
            CGenTLDevInfo device = m_ltDeviceList[cmbDeviceList.SelectedIndex];

            // ch:打开设备 | en:Open device
            if (null == m_MyCamera)
            {
                m_MyCamera = new CCamera();
                if (null == m_MyCamera)
                {
                    return;
                }
            }

            int nRet = m_MyCamera.CreateHandleByGenTL(ref device);
            if (CErrorDefine.MV_OK != nRet)
            {
                return;
            }

            nRet = m_MyCamera.OpenDevice();
            if (CErrorDefine.MV_OK != nRet)
            {
                m_MyCamera.DestroyHandle();
                ShowErrorMsg("Device open fail!", nRet);
                return;
            }

            // ch:设置采集连续模式 | en:Set Continues Aquisition Mode
            m_MyCamera.SetEnumValue("AcquisitionMode", (uint)MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            m_MyCamera.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

            // ch:控件操作 | en:Control operation
            SetCtrlWhenOpen();
        }

        private void SetCtrlWhenClose()
        {
            btnEnumInterface.Enabled = true;
            btnEnumDevice.Enabled = true;

            bnOpen.Enabled = true;

            bnClose.Enabled = false;
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = false;
            bnContinuesMode.Enabled = false;
            bnTriggerMode.Enabled = false;
            cbSoftTrigger.Enabled = false;
            bnTriggerExec.Enabled = false;
        }

        private void bnClose_Click(object sender, EventArgs e)
        {
            // ch:取流标志位清零 | en:Reset flow flag bit
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
            }

            // ch:关闭设备 | en:Close Device
            m_MyCamera.CloseDevice();
            m_MyCamera.DestroyHandle();

            // ch:控件操作 | en:Control Operation
            SetCtrlWhenClose();
        }

        private void bnContinuesMode_CheckedChanged(object sender, EventArgs e)
        {
            if (bnContinuesMode.Checked)
            {
                m_MyCamera.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
                cbSoftTrigger.Enabled = false;
                bnTriggerExec.Enabled = false;
            }
        }

        private void bnTriggerMode_CheckedChanged(object sender, EventArgs e)
        {
            // ch:打开触发模式 | en:Open Trigger Mode
            if (bnTriggerMode.Checked)
            {
                m_MyCamera.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);

                // ch:触发源选择:0 - Line0; | en:Trigger source select:0 - Line0;
                //           1 - Line1;
                //           2 - Line2;
                //           3 - Line3;
                //           4 - Counter;
                //           7 - Software;
                if (cbSoftTrigger.Checked)
                {
                    m_MyCamera.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                    if (m_bGrabbing)
                    {
                        bnTriggerExec.Enabled = true;
                    }
                }
                else
                {
                    m_MyCamera.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0);
                }
                cbSoftTrigger.Enabled = true;
            }
        }

        private void SetCtrlWhenStartGrab()
        {
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = true;

            if (bnTriggerMode.Checked && cbSoftTrigger.Checked)
            {
                bnTriggerExec.Enabled = true;
            }
        }

        public void ReceiveThreadProcess()
        {
            CFrameout pcFrameInfo = new CFrameout();
            CDisplayFrameInfo pcDisplayInfo = new CDisplayFrameInfo();

            while (m_bGrabbing)
            {
                int nRet = m_MyCamera.GetImageBuffer(ref pcFrameInfo, 1000);
                if (nRet == CErrorDefine.MV_OK)
                {
                    pcDisplayInfo.WindowHandle = pictureBox1.Handle;
                    pcDisplayInfo.Image = pcFrameInfo.Image;
                    m_MyCamera.DisplayOneFrame(ref pcDisplayInfo);
                    m_MyCamera.FreeImageBuffer(ref pcFrameInfo);
                }
                else
                {
                    if (bnTriggerMode.Checked)
                    {
                        Thread.Sleep(5);
                    }
                }
            }
        }

        private void bnStartGrab_Click(object sender, EventArgs e)
        {
            // ch:标志位置位true | en:Set position bit true
            m_bGrabbing = true;

            m_hReceiveThread = new Thread(ReceiveThreadProcess);
            m_hReceiveThread.Start();

            // ch:开始采集 | en:Start Grabbing
            int nRet = m_MyCamera.StartGrabbing();
            if (CErrorDefine.MV_OK != nRet)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
                ShowErrorMsg("Start Grabbing Fail!", nRet);
                return;
            }

            // ch:控件操作 | en:Control Operation
            SetCtrlWhenStartGrab();
        }

        private void cbSoftTrigger_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSoftTrigger.Checked)
            {
                // ch:触发源设为软触发 | en:Set trigger source as Software
                m_MyCamera.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
                if (m_bGrabbing)
                {
                    bnTriggerExec.Enabled = true;
                }
            }
            else
            {
                m_MyCamera.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0);
                bnTriggerExec.Enabled = false;
            }
        }

        private void bnTriggerExec_Click(object sender, EventArgs e)
        {
            // ch:触发命令 | en:Trigger command
            int nRet = m_MyCamera.SetCommandValue("TriggerSoftware");
            if (CErrorDefine.MV_OK != nRet)
            {
                ShowErrorMsg("Trigger Software Fail!", nRet);
            }
        }

        private void SetCtrlWhenStopGrab()
        {
            bnStartGrab.Enabled = true;
            bnStopGrab.Enabled = false;

            bnTriggerExec.Enabled = false;
        }

        private void bnStopGrab_Click(object sender, EventArgs e)
        {
            // ch:标志位设为false | en:Set flag bit false
            m_bGrabbing = false;
            m_hReceiveThread.Join();

            // ch:停止采集 | en:Stop Grabbing
            int nRet = m_MyCamera.StopGrabbing();
            if (nRet != CErrorDefine.MV_OK)
            {
                ShowErrorMsg("Stop Grabbing Fail!", nRet);
            }

            // ch:控件操作 | en:Control Operation
            SetCtrlWhenStopGrab();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            bnClose_Click(sender, e);
        }
    }
}