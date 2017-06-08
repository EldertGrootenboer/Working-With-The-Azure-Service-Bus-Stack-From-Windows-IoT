using System;
using System.Diagnostics;
using Windows.Devices.Gpio;

// Add reference to Universal Windows extension Windows IoT Extensions for the UWP to access GPIO

namespace Eldert.IoT.RaspberryPi2.FieldHub
{
    /// <summary>
    /// SHT15 Sensor Class, thanks goes out to https://github.com/krvarma/Windows-10-IoT-Core-and-SHT15
    /// </summary>
    class SHT15
    {
        private const double D1 = -40.0;  // for 14 Bit @ 5V
        private const double D2 = 0.01;  // for 14 Bit DEGC
        private const double D3 = 0.018;  // for 14 Bit DEGF

        private const double CC1 = -4.0;       // for 12 Bit
        private const double CC2 = 0.0405;    // for 12 Bit
        private const double CC3 = -0.0000028; // for 12 Bit
        private const double CT1 = 0.01;      // for 14 Bit @ 5V
        private const double CT2 = 0.00008;   // for 14 Bit @ 5V

        private readonly GpioPin _dataPin;
        private readonly GpioPin _sckPin;

        public SHT15(int dpn, int spn)
        {
            // Get GPIO Controller
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                Debug.WriteLine("Error opening GPIO");

                return;
            }

            // Get Pins
            _dataPin = gpio.OpenPin(dpn);
            _sckPin = gpio.OpenPin(spn);
        }

        // Read Raw Temperature
        public int ReadRawTemperature()
        {
            const int temperatureCommand = 3; // 00000011

            // Send Temperature Command
            SendSHTCommand(temperatureCommand);

            // Wait for result
            WaitForResult();

            // Read interger value
            var temperature = GetData16Bit();

            // Skip CRC
            SkipCrc();

            return temperature;
        }

        public double ReadHumidity(double temperature)
        {
            const int humidityCommand = 5; // 00000101

            // Send Temperature Command
            SendSHTCommand(humidityCommand);

            // Wait for result
            WaitForResult();

            // Read interger value
            var val = GetData16Bit();

            // Skip CRC
            SkipCrc();

            // Calculate Humiduty
            var linearHumidity = CC1 + CC2 * val + CC3 * val * val;
            var correctedHumidity = (temperature - 25.0) * (CT1 + CT2 * val) + linearHumidity;

            return correctedHumidity;
        }

        // Returns Temperature in C
        public double ReadTemperatureC()
        {
            return CalculateTemperatureC(ReadRawTemperature());
        }

        // Returns Temperature in F
        public double ReadTemperatureF()
        {
            return CalculateTemperatureF(ReadRawTemperature());
        }

        // Calculate Temperature in C
        public double CalculateTemperatureC(int temperature)
        {
            return CalculateTemperature(temperature, D2);
        }

        // Calculate Temperature in F
        public double CalculateTemperatureF(int temperature)
        {
            return CalculateTemperature(temperature, D3);
        }

        // Calculate Temperature in, if mult is D2 then in C, if mult is D3 then in F
        public double CalculateTemperature(int rawTemperature, double mult)
        {
            return ((rawTemperature * mult) + D1);
        }

        public double DewPoint(double celsius, double humidity)
        {
            // (1) Saturation Vapor Pressure = ESGG(T)
            var ratio = 373.15 / (273.15 + celsius);
            var rhs = -7.90298 * (ratio - 1);
            rhs += 5.02808 * Math.Log10(ratio);
            rhs += -1.3816e-7 * (Math.Pow(10, (11.344 * (1 - 1 / ratio))) - 1);
            rhs += 8.1328e-3 * (Math.Pow(10, (-3.49149 * (ratio - 1))) - 1);
            rhs += Math.Log10(1013.246);

            // factor -3 is to adjust units - Vapor Pressure SVP * humidity
            var vp = Math.Pow(10, rhs - 3) * humidity;

            // (2) DEWPOINT = F(Vapor Pressure)
            var T = Math.Log(vp / 0.61078);   // temp var
            return (241.88 * T) / (17.558 - T);
        }

        // Cleanup
        public void Dispose()
        {
            _dataPin.Dispose();
            _sckPin.Dispose();
        }

        // Send SHT Command
        private void SendSHTCommand(int command)
        {
            // Set pin mode
            DataPinModeOutput();
            SckPinModeOutput();

            // Start Transmission
            DataHigh();
            SckHigh();
            DataLow();
            SckLow();
            SckHigh();
            DataHigh();
            SckLow();

            // Shift Out Send command 
            ShiftOut(command);

            // Set Data Pin Mode
            DataPinModeInput();

            // Check ACKs
            SckHigh();
            
            if (GpioPinValue.Low != ReadDataPin())
            {
                Debug.WriteLine("Error 1001");
            }

            SckLow();

            if (GpioPinValue.High != ReadDataPin())
            {
                Debug.WriteLine("Error 1002");
            }
        }

        // Set SCK Pin Mode Input
        // ReSharper disable once UnusedMember.Local
        private void SckPinModeInput()
        {
            _sckPin.SetDriveMode(GpioPinDriveMode.Input);
        }

        // Set SCK Pin Mode Output
        private void SckPinModeOutput()
        {
            _sckPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        // Set Data Pin Mode Input
        private void DataPinModeInput()
        {
            _dataPin.SetDriveMode(GpioPinDriveMode.Input);
        }

        // Set Data Pin Mode Output
        private void DataPinModeOutput()
        {
            _dataPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        // Read Data Pin
        private GpioPinValue ReadDataPin()
        {
            return _dataPin.Read();
        }

        // Set SCK to High
        private void SckHigh()
        {
            _sckPin.Write(GpioPinValue.High);
        }

        // Set SCK to Low
        private void SckLow()
        {
            _sckPin.Write(GpioPinValue.Low);
        }

        // Set Data to High
        private void DataHigh()
        {
            _dataPin.Write(GpioPinValue.High);
        }

        // Set Data to Low
        private void DataLow()
        {
            _dataPin.Write(GpioPinValue.Low);
        }

        // Shit Out
        private void ShiftOut(long command)
        {
            DataPinModeOutput();
            SckPinModeOutput();

            for (var i = 0; i < 8; ++i)
            {
                var bit = (command & (1 << (7 - i)));

                if (bit == 0) DataLow();
                else DataHigh();

                SckHigh();
                SckLow();
            }
        }

        // Shift In
        private int ShiftIn(int bits)
        {
            DataPinModeInput();
            SckPinModeOutput();

            var retVal = 0;

            for (var i = 0; i < bits; ++i)
            {
                SckHigh();
                var pinvalue = ReadDataPin();

                var val = (pinvalue == GpioPinValue.High ? 1 : 0);

                retVal = (retVal * 2) + val;
                SckLow();
            }

            return retVal;
        }

        // Wait until result is ready or max iteration reached
        private void WaitForResult()
        {
            DataPinModeInput();

            int i;

            for (i = 0; i < 20000; ++i)
            {
                var ack = _dataPin.Read();

                if (GpioPinValue.Low == ack)
                {
                    break;
                }
            }

            //Debug.WriteLine("Max Loop: " + i);
        }

        // Read 16bit integer value
        private int GetData16Bit()
        {
            // Set Pin Modes
            DataPinModeInput();
            SckPinModeOutput();

            int data;

            // Read data
            data = ShiftIn(8);
            data *= 256;

            // Set Data Pin Mode
            DataPinModeOutput();

            DataHigh();
            DataLow();
            SckHigh();
            SckLow();

            DataPinModeInput();

            // Read Data
            data |= ShiftIn(8);

            return data;
        }

        // Skip CRC
        public void SkipCrc()
        {
            DataPinModeOutput();
            SckPinModeOutput();

            DataHigh();
            SckHigh();
            SckLow();
        }
    }
}
