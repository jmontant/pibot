using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;


namespace LSM303DLM
{
    /// <summary>
    /// Sample app to read from LSM303DLM Acceleration/Magnetometer sensor via I2C interface.
    /// Written by Paul Bammel  March 2017
    /// </summary>
    
    struct Acceleration
    {
        public double Ax;
        public double Ay;
        public double Az;
    };

    struct Magnetometer
    {
        public double Mx;
        public double My;
        public double Mz;
    }

    public sealed partial class MainPage : Page
    {
        private const byte ACCEL_I2C_ADDR = 0x18;           /* 7-bit I2C address of accelerometer */
        private const byte ACCEL_NORMAL = 0x27;             /* Normal Power, X,Y,Z axis enabled */
        private const byte ACCEL_BLE = 0x40;                /* Continuous Read, big-endian structure */


        private const byte CTRL_REG1_A = 0x20;              /* CTRL_REG1_A register */
        private const byte CTRL_REG4_A = 0x23;              /* CTRL_REG4_A register */
        private const byte OUT_X_L_A = 0x28;                /* OUT_X_L_A register */
        private const byte OUT_Y_L_A = 0x2A;                /* OUT_Y_L_A register */
        private const byte OUT_Z_L_A = 0x2C;                /* OUT_Z_L_A register */

        private const byte MAG_I2C_ADDR = 0x1E;             /* 7-bit I2C address of magnetrometer */
        private const byte MAG_ODR30 = 0x14;                /* Set ODR data rate to 30 Hz */
        private const byte MAG_CONT = 0x00;                 /* Set magnetrometer to continuous mode*/

        private const byte CRA_REG_M = 0x00;                /* CRA_REG_M register */
        private const byte MR_REG_M = 0x02;                 /* MR_REG_M register */
        private const byte OUT_X_H_M = 0x03;                /* OUT_X_H_M register */
        private const byte OUT_Z_H_M = 0x05;                /* OUT_Z_H_M register */
        private const byte OUT_Y_H_M = 0x07;                /* OUT_Y_H_M register */

        private I2cDevice I2CAccel;
        private I2cDevice I2CMag;

        private Timer periodicTimer;

        public MainPage()
        {
            this.InitializeComponent();

            /* Register for the unloaded event so we can clean up upon exit */
            Unloaded += MainPage_Unloaded;

            /* Initialize the I2C bus, accelerometer, and timer */
            InitI2CAccel();
            InitI2CMag();

            /* Now that everything is initialized, create a timer so we read data every 100mS */
            Timer periodicTimer = new Timer(this.TimerCallback, null, 0, 100);
            periodicTimer = new Timer(this.TimerCallback, null, 0, 100);

        }

        private async void InitI2CAccel()
        {

            var settings = new I2cConnectionSettings(ACCEL_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var controller = await I2cController.GetDefaultAsync();
            I2CAccel = controller.GetDevice(settings);    /* Create an I2cDevice with our selected bus controller and I2C settings */


            /* 
             * Initialize the accelerometer:
             *
             * For this device, we create 2-byte write buffers:
             * The first byte is the register address we want to write to.
             * The second byte is the contents that we want to write to the register. 
             */
            byte[] WriteBuf_AccelNormal = new byte[] { CTRL_REG1_A, ACCEL_NORMAL };
            byte[] WriteBuf_BLE = new byte[] { CTRL_REG4_A, ACCEL_BLE };

            /* Write the register settings */
            try
            {
                I2CAccel.Write(WriteBuf_AccelNormal);
                I2CAccel.Write(WriteBuf_BLE);
            }
            /* If the write fails display the error and stop running */
            catch (Exception ex)
            {
                AccelText_Status.Text = "Failed to communicate with device: " + ex.Message;
                return;
            }

        }

        private async void InitI2CMag()
        {

            var settings = new I2cConnectionSettings(MAG_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var controller = await I2cController.GetDefaultAsync();
            I2CMag = controller.GetDevice(settings);    /* Create an I2cDevice with our selected bus controller and I2C settings */

            /* 
             * Initialize the magnetometer:
             *
             * For this device, we create 2-byte write buffers:
             * The first byte is the register address we want to write to.
             * The second byte is the contents that we want to write to the register. 
             */
            byte[] WriteBuf_ODR30 = new byte[] { CRA_REG_M, MAG_ODR30 };
            byte[] WriteBuf_ContMode = new byte[] { MR_REG_M, MAG_CONT };

            /* Write the register settings */
            try
            {
                I2CAccel.Write(WriteBuf_ODR30);
                I2CAccel.Write(WriteBuf_ContMode);
            }
            /* If the write fails display the error and stop running */
            catch (Exception ex)
            {
                MagText_Status.Text = "Failed to communicate with device: " + ex.Message;
                return;
            }

        }
        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            I2CAccel.Dispose();
            I2CMag.Dispose();
        }

        private void TimerCallback(object state)
        {
            string AxText, AyText, AzText;
            string MxText, MyText, MzText;
            string AstatusText;
            string MstatusText;

            /* Read and format accelerometer data */
            try
            {
                Acceleration accel = ReadI2CAccel();
                AxText = String.Format("X Axis: {0:F3}G", accel.Ax);
                AyText = String.Format("Y Axis: {0:F3}G", accel.Ay);
                AzText = String.Format("Z Axis: {0:F3}G", accel.Az);
                AstatusText = "Status: Running";
            }
            catch (Exception ex)
            {
                AxText = "X Axis: Error";
                AyText = "Y Axis: Error";
                AzText = "Z Axis: Error";
                AstatusText = "Failed to read from Accelerometer: " + ex.Message;
            }

            /* Read and format magnetometer data */
            try
            {
                Magnetometer mag = ReadI2CMag();
                MxText = String.Format("X Axis: {0:F3}G", mag.Mx);
                MyText = String.Format("Y Axis: {0:F3}G", mag.My);
                MzText = String.Format("Z Axis: {0:F3}G", mag.Mz);
                MstatusText = "Status: Running";
            }
            catch (Exception ex)
            {
                MxText = "X Axis: Error";
                MyText = "Y Axis: Error";
                MzText = "Z Axis: Error";
                MstatusText = "Failed to read from Magnetometer: " + ex.Message;
            }

            /* UI updates must be invoked on the UI thread */
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                AccelText_X_Axis.Text = AxText;
                AccelText_Y_Axis.Text = AyText;
                AccelText_Z_Axis.Text = AzText;
                AccelText_Status.Text = AstatusText;

                MagText_X_Axis.Text = MxText;
                MagText_Y_Axis.Text = MyText;
                MagText_Z_Axis.Text = MzText;
                MagText_Status.Text = MstatusText;
            });
        }

        private Acceleration ReadI2CAccel()
        {
            const int ACCEL_RES = 4096;         /* The LSM303 has 12 bit resolution giving 4096 unique values                     */
            const int ACCEL_DYN_RANGE_G = 8;    /* The LSM3035 had a total dynamic range of 8G, since we're configuring it to +-4G */
            const int UNITS_PER_G = ACCEL_RES / ACCEL_DYN_RANGE_G;  /* Ratio of raw int values to G units                          */

            byte[] RegAddrBuf = new byte[] { OUT_X_L_A };   /* Register address we want to read from                                         */
            byte[] ReadBuf = new byte[6];                   /* We read 6 bytes sequentially to get all 3 two-byte axes registers in one read */

            /* 
             * Read from the accelerometer 
             * We call WriteRead() so we first write the address of the X-Axis I2C register, then read all 3 axes
             */
            I2CAccel.WriteRead(RegAddrBuf, ReadBuf);

            /* 
             * In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes from the I2C read for each axis.
             * We accomplish this by using the BitConverter class.
             */
            short AccelerationRawX = BitConverter.ToInt16(ReadBuf, 0);
            short AccelerationRawY = BitConverter.ToInt16(ReadBuf, 2);
            short AccelerationRawZ = BitConverter.ToInt16(ReadBuf, 4);

            /* Convert raw values to G's */
            Acceleration accel;
            accel.Ax = (double)AccelerationRawX / UNITS_PER_G;
            accel.Ay = (double)AccelerationRawY / UNITS_PER_G;
            accel.Az = (double)AccelerationRawZ / UNITS_PER_G;

            return accel;
        }

        private Magnetometer ReadI2CMag()
        {

            byte[] RegAddr = new byte[] { OUT_X_H_M };      /* Register address we want to read from                                         */
            byte[] ReadMBuf = new byte[6];                  /* We read 6 bytes sequentially to get all 3 two-byte axes registers in one read */

            /* 
             * Read from the magnetometer 
             * We call WriteRead() so we first write the address of the X-Axis I2C register, then read all 3 axes
             */
            I2CAccel.WriteRead(RegAddr, ReadMBuf);

            /* 
             * In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes from the I2C read for each axis.
             * We accomplish this by using the BitConverter class.
             */
            short MagnetometerRawX = BitConverter.ToInt16(ReadMBuf, 0);
            short MagnetometerRawY = BitConverter.ToInt16(ReadMBuf, 4);
            short MagnetometerRawZ = BitConverter.ToInt16(ReadMBuf, 2);

            /* Convert raw values to G's */
            Magnetometer Mag;
            Mag.Mx = (double)MagnetometerRawX;
            Mag.My = (double)MagnetometerRawY;
            Mag.Mz = (double)MagnetometerRawZ;

            return Mag;
        }
    }
}
