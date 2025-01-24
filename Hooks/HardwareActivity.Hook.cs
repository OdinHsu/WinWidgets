using Services;
using System.Timers;
using HardwareInfoDll;  // 引用 C++/CLI DLL

namespace Hooks
{
    internal class HardwareActivityHook
    {
        private TimerService timerService = new TimerService();
        private HardwareService hardwareService = new HardwareService();

        // CPU info event
        public delegate void CPUInfoHandler(string cpuInfo);
        public event CPUInfoHandler OnCPUInfo;

        // GPU info event
        public delegate void GPUInfoHandler(string gpuInfo);
        public event GPUInfoHandler OnGPUInfo;

        // RAM info event
        public delegate void RAMInfoHandler(string ramInfo);
        public event RAMInfoHandler OnRAMInfo;

        // Storage info event
        public delegate void DiskInfoHandler(string diskInfo);
        public event DiskInfoHandler OnDiskInfo;

        // Network info event
        public delegate void NetworkInfoHandler(string networkInfo);
        public event NetworkInfoHandler OnNetworkInfo;

        // 建立 HardwareInfo 物件
        HardwareInfo hardwareInfo;

        public HardwareActivityHook() 
        {
            hardwareInfo = new HardwareInfo();
            hardwareInfo.StartSaveAllHardwareThread(1500);

            this.timerService.CreateTimer(1500, OnHardwareInfoEvent, true, true);
        }

        private void OnHardwareInfoEvent(object sender, ElapsedEventArgs e)
        {
            // CPU info event
            string cpuInfo = this.hardwareService.GetCPUInfo(hardwareInfo);
            OnCPUInfo?.Invoke(cpuInfo);

            // GPU info event
            string gpuInfo = this.hardwareService.GetAllGPUInfo(hardwareInfo);
            OnGPUInfo?.Invoke(gpuInfo);

            // RAM info event
            string ramInfo = this.hardwareService.GetRAMInfo(hardwareInfo);
            OnRAMInfo?.Invoke(ramInfo);

            // Storage info event
            string diskInfo = this.hardwareService.GetDiskInfo(hardwareInfo);
            OnDiskInfo?.Invoke(diskInfo);

            // Network info event
            string networkInfo = this.hardwareService.GetNetworkInfo(hardwareInfo);
            OnNetworkInfo?.Invoke(networkInfo);
        }
    }
}
