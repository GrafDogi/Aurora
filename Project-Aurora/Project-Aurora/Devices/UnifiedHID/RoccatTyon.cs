﻿using HidLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.Devices.UnifiedHID
{

    internal class RoccatTyon : UnifiedBase
    {
        private static HidDevice ctrl_device_leds;
        private static HidDevice ctrl_device;

        public RoccatTyon()
        {
            PrettyName = "Roccat Tyon";
        }

        private bool InitMouseColor()
        {
            return ctrl_device.WriteFeatureData(initPacket);
        }

        static bool WaitCtrlDevice()
        {
            for (int i = 1; i < 100; i++) // If still fails after 100 tries then timeout
            {
                // 150ms is the magic number here, should suffice on first try.
                Thread.Sleep(150);
                if (ctrl_device.ReadFeatureData(out byte[] buffer, 0x04) && buffer.Length > 2)
                {
                    if (buffer[1] == 0x01)
                        return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public override bool Connect()
        {
            if (!Global.Configuration.VarRegistry.GetVariable<bool>($"UnifiedHID_{this.GetType().Name}_enable"))
            {
                return false;
            }
            IEnumerable<HidDevice> devices = HidDevices.Enumerate(0x1E7D, new int[] { 0x2E4A });
            try
            {
                if (devices.Count() > 0)
                {
                    ctrl_device_leds = devices.First(dev => dev.Capabilities.UsagePage == 0x0001 && dev.Capabilities.Usage == 0x0002);
                    ctrl_device = devices.First(dev => dev.Capabilities.FeatureReportByteLength > 50);

                    ctrl_device.OpenDevice();
                    ctrl_device_leds.OpenDevice();
                    bool success = InitMouseColor() && WaitCtrlDevice();
                    if (!success)
                    {
                        Global.logger.LogLine($"Roccat Tyon Could not connect\n", Logging_Level.Error);
                        ctrl_device.CloseDevice();
                        ctrl_device_leds.CloseDevice();
                    }
                    Global.logger.LogLine($"Roccat Tyon Connected\n", Logging_Level.Info);
                    return (IsConnected = success);
                }
            }
            catch (Exception exc)
            {
                Global.logger.LogLine($"Error when attempting to open UnifiedHID device:\n{exc}", Logging_Level.Error);
            }
            return false;
        }

        // We need to override Disconnect() too cause we have two HID devices open for this keyboard.
        public override bool Disconnect()
        {
            try
            {
                ctrl_device.CloseDevice();
                ctrl_device_leds.CloseDevice();
                return true;
            }
            catch (Exception exc)
            {
                Global.logger.LogLine($"Error when attempting to close UnifiedHID device:\n{exc}", Logging_Level.Error);
            }
            return false;
        }

        public override bool SetLEDColour(DeviceKeys key, byte red, byte green, byte blue)
        {
            try
            {
                if (!this.IsConnected)
                    return false;
                byte[] hwmap = new byte[11];

                hwmap[0] = red;
                hwmap[1] = green;
                hwmap[2] = blue;
                hwmap[3] = 0x00;
                hwmap[4] = 0x00;
                hwmap[5] = red;
                hwmap[6] = green;
                hwmap[7] = blue;
                hwmap[8] = 0x00;
                hwmap[9] = 0x80;
                hwmap[10] = 0x80;

                byte[] workbuf = new byte[30];
                Array.Copy(controlPacket, 0, workbuf, 0, 19);
                Array.Copy(hwmap, 0, workbuf, 19, 11);

                unsafe
                {
                    fixed (byte* workbufPointer = workbuf)
                    {
                        if (ctrl_device.WriteFeatureData(workbuf) != true)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception exc)
            {
                Global.logger.LogLine($"Error when attempting to close UnifiedHID device:\n{exc}", Logging_Level.Error);
                return false;
            }
        }

        // Packet with values set to white for mouse initialisation.
        static readonly byte[] initPacket = new byte[] {
            0x06,0x1e,0x00,0x00,
            0x06,0x06,0x06,0x10,0x20,0x40,0x80,0xa4,0x02,0x03,0x33,0x00,0x01,0x01,0x03,
            0xff,0xff,0xff,0x00,0x00,0xff,0xff,0xff,0x00,0x01,0x08
        };

        // Packet with fixed values for affixing to mouse colors.
        static readonly byte[] controlPacket = new byte[] {
            0x06,0x1e,0x00,0x00,
            0x06,0x06,0x06,0x10,0x20,0x40,0x80,0xa4,0x02,0x03,0x33,0x00,0x01,0x01,0x03
        };
    }
}