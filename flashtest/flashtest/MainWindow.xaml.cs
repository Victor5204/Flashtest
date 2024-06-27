using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace flashtest
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadUSBDevices();
        }

        private void LoadUSBDevices()
        {
            var usbDevices = GetUSBDevices();
            usbComboBox.ItemsSource = usbDevices;
            usbComboBox.DisplayMemberPath = "Description"; // Изменено на "Description"
        }

        private List<USBDeviceInfo> GetUSBDevices()
        {
            List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_DiskDrive Where InterfaceType='USB'"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                string deviceID = (string)device.GetPropertyValue("DeviceID");
                string pnpDeviceID = (string)device.GetPropertyValue("PNPDeviceID");
                string description = (string)device.GetPropertyValue("Model");

                // Получаем букву диска
                var partitionSearcher = new ManagementObjectSearcher(
                    $"associators of {{Win32_DiskDrive.DeviceID='{deviceID}'}} where AssocClass=Win32_DiskDriveToDiskPartition");
                var partitionCollection = partitionSearcher.Get();

                foreach (var partition in partitionCollection)
                {
                    var logicalDiskSearcher = new ManagementObjectSearcher(
                        $"associators of {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} where AssocClass=Win32_LogicalDiskToPartition");
                    var logicalDiskCollection = logicalDiskSearcher.Get();

                    foreach (var logicalDisk in logicalDiskCollection)
                    {
                        devices.Add(new USBDeviceInfo(deviceID, pnpDeviceID, description, (string)logicalDisk["DeviceID"]));
                    }
                }
            }

            collection.Dispose();
            return devices;
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (usbComboBox.SelectedItem is USBDeviceInfo selectedDevice)
            {
                string driveLetter = selectedDevice.DriveLetter;
                string testFilePath = Path.Combine(driveLetter, "testfile.dat");

                try
                {
                    // Тестирование скорости записи
                    var writeSpeed = await MeasureWriteSpeed(testFilePath);

                    // Тестирование скорости чтения
                    var readSpeed = await MeasureReadSpeed(testFilePath);

                    MessageBox.Show($"Скорость записи: {writeSpeed:F2} MB/s\nСкорость чтения: {readSpeed:F2} MB/s", "Тестирование", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Ошибка доступа к файлу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Удаление тестового файла после завершения тестирования
                    if (File.Exists(testFilePath))
                    {
                        File.Delete(testFilePath);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите устройство для тестирования.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<double> MeasureWriteSpeed(string filePath)
        {
            const int bufferSize = 1024 * 1024; // 1 MB
            byte[] buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                for (int i = 0; i < 10; i++) // Запись 10 MB данных
                {
                    await fileStream.WriteAsync(buffer, 0, buffer.Length);
                }

                stopwatch.Stop();
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                double writeSpeed = (10.0 / elapsedSeconds); // MB/s
                return writeSpeed;
            }
        }

        private async Task<double> MeasureReadSpeed(string filePath)
        {
            const int bufferSize = 1024 * 1024; // 1 MB
            byte[] buffer = new byte[bufferSize];

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                for (int i = 0; i < 10; i++) // Чтение 10 MB данных
                {
                    await fileStream.ReadAsync(buffer, 0, buffer.Length);
                }

                stopwatch.Stop();
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                double readSpeed = (10.0 / elapsedSeconds); // MB/s
                return readSpeed;
            }
          }
        }

        public class USBDeviceInfo
        {
            public string DeviceID { get; }
            public string PnpDeviceID { get; }
            public string Description { get; }
            public string DriveLetter { get; }

            public USBDeviceInfo(string deviceID, string pnpDeviceID, string description, string driveLetter)
            {
                DeviceID = deviceID;
                PnpDeviceID = pnpDeviceID;
                Description = description;
                DriveLetter = driveLetter;
            }
        }
    }



