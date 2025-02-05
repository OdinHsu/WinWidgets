using Services;
using System.Timers;
using HardwareInfoDll;
using System.Threading.Tasks;
using System.Threading;
using System;  // 引用 C++/CLI DLL

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

        private readonly System.Threading.Timer timer;
        private CancellationTokenSource cts;

        public HardwareActivityHook()
        {
            hardwareInfo = new HardwareInfo();

            //this.timerService.CreateTimer(1000, OnHardwareInfoEvent, true, true);
            cts = new CancellationTokenSource();
            timer = new System.Threading.Timer(OnHardwareInfoEvent, null, 0, 1000);
        }

        //private void OnHardwareInfoEvent(object sender, ElapsedEventArgs e)
        private void OnHardwareInfoEvent(object state)
        {
            try
            {
                cts.Cancel();
                cts = new CancellationTokenSource();
                var token = cts.Token;

                Task.Run(async () =>
                {
                    // 异步保存硬件信息（假设 SaveAllHardwareAsync 存在）
                    hardwareInfo.SaveAllHardware();

                    // 获取所有硬件信息（合并到同一后台任务）
                    var networkInfo = hardwareService.GetNetworkInfo(hardwareInfo);
                    var cpuInfo = hardwareService.GetCPUInfo(hardwareInfo);
                    var gpuInfo = hardwareService.GetAllGPUInfo(hardwareInfo);
                    var ramInfo = hardwareService.GetRAMInfo(hardwareInfo);
                    var diskInfo = hardwareService.GetDiskInfo(hardwareInfo);

                    // 在 UI 线程触发事件
                    await Task.Run(() =>
                    {
                        OnNetworkInfo?.Invoke(networkInfo);
                        OnCPUInfo?.Invoke(cpuInfo);
                        OnGPUInfo?.Invoke(gpuInfo);
                        OnRAMInfo?.Invoke(ramInfo);
                        OnDiskInfo?.Invoke(diskInfo);
                    }, token);
                }, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
