﻿using Services;
using System.Timers;

namespace Hooks
{
    internal class HardwareActivityHook
    {
        public delegate void BatteryLevelHandler(string level);
        public delegate void SpaceAvailableInDriveHandler(long freeSpace);
        public delegate void AnyFullscreenApplicationHandler(bool fullscreen);
        public event BatteryLevelHandler OnBatteryLevel;
        public event SpaceAvailableInDriveHandler OnSpaceAvailable;
        public event AnyFullscreenApplicationHandler OnAnyApplicationFullscrenStatusChanged;

        private bool lastAnyApplicationFullscreenStatus = false;

        private TimerService timerService = new TimerService();
        private HardwareService hardwareService = new HardwareService();

        // CPU info event
        public delegate void CPUInfoHandler(string cpuInfo);
        public event CPUInfoHandler OnCPUInfo;

        // GPU info event
        public delegate void GPUInfoHandler(string gpuInfo);
        public event GPUInfoHandler OnGPUInfo;

        public HardwareActivityHook() 
        {
            this.timerService.CreateTimer(1000, OnBatteryLevelEvent, true, true);
            this.timerService.CreateTimer(1000, OnSpaceAvailableInDrivesEvent, true, true);
            //this.timerService.CreateTimer(1000, OnAnyApplicationFullscreenStatusEvent, true, true);
            this.timerService.CreateTimer(1000, OnCPUInfoEvent, true, true);
            this.timerService.CreateTimer(1000, OnGPUInfoEvent, true, true);
        }

        private void OnGPUInfoEvent(object sender, ElapsedEventArgs e)
        {
            string gpuInfo = this.hardwareService.GetAllGPUInfo();
            OnGPUInfo?.Invoke(gpuInfo);
        }

        private void OnCPUInfoEvent(object sender, ElapsedEventArgs e)
        {
            string cpuInfo = this.hardwareService.GetCPUInfo();
            OnCPUInfo?.Invoke(cpuInfo);
        }

        private void OnBatteryLevelEvent(object sender, ElapsedEventArgs e)
        {
            string level = this.hardwareService.GetBatteryLevelPercentage();
            OnBatteryLevel.Invoke(level);
        }

        private void OnSpaceAvailableInDrivesEvent(object sender, ElapsedEventArgs e)
        {
            long freeSpace = this.hardwareService.GetFreeSpaceAvailableInDrive("C");
            OnSpaceAvailable.Invoke(freeSpace);
        }

        private void OnAnyApplicationFullscreenStatusEvent(object sender, ElapsedEventArgs e)
        {
            bool fullscreenStatus = HardwareService.isAnyApplicationFullscreen();

            if (fullscreenStatus != this.lastAnyApplicationFullscreenStatus)
            {
                this.lastAnyApplicationFullscreenStatus = fullscreenStatus;
                OnAnyApplicationFullscrenStatusChanged.Invoke(fullscreenStatus);
            }
        }
    }
}
