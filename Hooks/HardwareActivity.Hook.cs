using Services;
using System.Timers;
using HardwareInfoDll;  // 引用 C++/CLI DLL

namespace Hooks
{
    internal class HardwareActivityHook
    {
        public delegate void BatteryLevelHandler(string level);
        public delegate void SpaceAvailableInDriveHandler(long freeSpace);
        public event BatteryLevelHandler OnBatteryLevel;
        public event SpaceAvailableInDriveHandler OnSpaceAvailable;

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

        // 建立 HardwareInfo 物件
        HardwareInfo hardwareInfo;

        public HardwareActivityHook() 
        {
            hardwareInfo = new HardwareInfo();
            hardwareInfo.StartSaveAllHardwareThread(800);

            this.timerService.CreateTimer(1000, OnHardwareInfoEvent, true, true);
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

            // battery level event
            string level = this.hardwareService.GetBatteryLevelPercentage();
            OnBatteryLevel?.Invoke(level);

            // space available in drive event
            long freeSpace = this.hardwareService.GetFreeSpaceAvailableInDrive("C");
            OnSpaceAvailable?.Invoke(freeSpace);
        }
    }
}
