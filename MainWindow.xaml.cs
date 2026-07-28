/* 
 * QuachArrow
 * Утилита для управления стрелками ярлыков Windows
 * Разработано: Kirill | GreenPer Team
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QuachArrow
{
    public partial class MainWindow : Window
    {
        // Путь к системному реестру для настройки иконок ярлыков
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Управление окном (UI)

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Позволяет перетаскивать окно за любую область
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        #endregion

        #region Основная логика реестра

        private void BtnRemoveArrows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string blankIconPath = Path.Combine(winDir, "QuachBlank256.ico");

                // Программно генерируем прозрачную иконку 256x256
                GenerateTransparentIcon(blankIconPath);

                // Записываем путь в реестр
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistryPath, true))
                {
                    key?.SetValue("29", $@"{blankIconPath},0", RegistryValueKind.String);
                }

                HardRestartExplorer();

                MessageBox.Show("Стрелки успешно убраны! Теперь точно без черных квадратов.",
                                "QuachArrow", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestoreArrows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        if (key.GetValue("29") != null)
                            key.DeleteValue("29");

                        if (key.ValueCount == 0 && key.SubKeyCount == 0)
                        {
                            key.Close();
                            Registry.LocalMachine.DeleteSubKey(RegistryPath, false);
                        }
                    }
                }

                HardRestartExplorer();

                MessageBox.Show("Стрелки успешно восстановлены!", "QuachArrow", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Системные функции генерации и перезапуска

        /// <summary>
        /// Генерация 100% валидного пустого .ico файла размером 256x256 пикселей на движке WPF.
        /// Исключает появление бага с "черным квадратом" на Windows 11.
        /// </summary>
        private void GenerateTransparentIcon(string path)
        {
            if (File.Exists(path)) return;

            // 1. Создаем прозрачный холст 256x256
            var bmp = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            // 2. Упаковываем PNG в правильный системный .ico файл с заголовками
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);  // reserved
                bw.Write((short)1);  // type (1 = ico)
                bw.Write((short)1);  // count of images
                bw.Write((byte)0);   // width (0 означает 256)
                bw.Write((byte)0);   // height (0 означает 256)
                bw.Write((byte)0);   // colors
                bw.Write((byte)0);   // reserved
                bw.Write((short)1);  // color planes
                bw.Write((short)32); // bpp
                bw.Write(pngBytes.Length); // size of image
                bw.Write(22);        // offset to image data
                bw.Write(pngBytes);  // записываем саму картинку
            }
        }

        /// <summary>
        /// Жесткая перезагрузка проводника с удалением заблокированного кэша иконок
        /// </summary>
        private void HardRestartExplorer()
        {
            try
            {
                // Убиваем процесс проводника
                foreach (Process process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit();
                }

                // Чистим кэш
                ClearIconCache();

                // Воскрешаем проводник
                string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                Process.Start(explorerPath);
            }
            catch { }
        }

        /// <summary>
        /// Удаление IconCache.db и файлов кэша из локальной директории пользователя
        /// </summary>
        private void ClearIconCache()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                // Удаляем основной кэш
                string iconCacheDb = Path.Combine(localAppData, "IconCache.db");
                if (File.Exists(iconCacheDb)) File.Delete(iconCacheDb);

                // Удаляем кэш из папки Explorer (актуально для Win 10/11)
                string explorerPath = Path.Combine(localAppData, @"Microsoft\Windows\Explorer");
                if (Directory.Exists(explorerPath))
                {
                    foreach (string file in Directory.GetFiles(explorerPath, "iconcache*"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        #endregion
    }
}