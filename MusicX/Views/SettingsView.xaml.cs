﻿using DryIoc;
using MusicX.Core.Services;
using MusicX.Models;
using MusicX.Services;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Storage;

namespace MusicX.Views
{
    /// <summary>
    /// Логика взаимодействия для SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Page
    {
        private readonly ConfigService configService;
        private ConfigModel config;
        private readonly VkService vkService;
        public SettingsView(ConfigService configService)
        {
            InitializeComponent();

            this.vkService = StaticService.Container.Resolve<VkService>();

            this.configService = configService;

            this.Loaded += SettingsView_Loaded;
            
        }

        private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            this.config = await configService.GetConfig();

            UserName.Text = config.UserName.Split(' ')[0];

            var usr = await vkService.GetCurrentUserAsync();

            UserImage.ImageSource = new BitmapImage(usr.Photo200);

            var path = $"{AppDomain.CurrentDomain.BaseDirectory}/logs";

            DirectoryInfo di = new DirectoryInfo(path);

            double memory = 0;

            foreach (FileInfo file in di.GetFiles())
            {
                memory += file.Length / 1024;
            }

            if (memory > 1024)
            {
                memory /= 1024;
                MemoryType.Text = "МБ";
            }else
            {
                MemoryType.Text = "КБ";

            }

            memory = Math.Round(memory, 2);
            MemoryLogs.Text = memory.ToString();
        }

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            config.AccessToken = null;
            config.UserName = null;
            config.UserId = 0;

            //await configService.SetConfig(config);

            var logger = StaticService.Container.Resolve<Logger>();
            var navigation = StaticService.Container.Resolve<Services.NavigationService>();

            new LoginWindow(vkService, configService, logger, navigation).Show();

            navigation.CloseRootWindow();
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TelegramButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/MusicXPlayer",
                UseShellExecute = true
            });
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}/logs";

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private void RemoveLogs_Click(object sender, RoutedEventArgs e)
        {
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}/logs";

            DirectoryInfo di = new DirectoryInfo(path);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            MemoryLogs.Text = "0";
            MemoryType.Text = "КБ";


        }
    }
}