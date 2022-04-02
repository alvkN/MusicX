﻿using DryIoc;
using MusicX.Core.Services;
using MusicX.Services;
using MusicX.ViewModels;
using NLog;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace MusicX.Views
{
    /// <summary>
    /// Логика взаимодействия для StartingWindow.xaml
    /// </summary>
    public partial class StartingWindow : Window
    {
        public StartingWindow()
        {
            InitializeComponent();
           // this.Background = Brushes.Black;

        }

        private Container container;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {

            await Task.Run(async () =>
            {
                container = new Container();
                container.Register<VkService>(Reuse.Singleton);
                container.Register<ServerService>(Reuse.Singleton);
                container.Register<GithubService>(Reuse.Singleton);
                container.Register<DiscordService>(Reuse.Singleton);
                container.RegisterInstance<Logger>(LogManager.Setup().GetLogger("Common"));

                container.Register<SectionViewModel>(Reuse.Singleton);
                container.Register<PlaylistViewModel>(Reuse.Singleton);

                container.Register<NavigationService>(Reuse.Singleton);
                container.Register<ConfigService>(Reuse.Singleton);
                container.Register<PlayerService>(Reuse.Singleton);
                StaticService.Container = container;

                var vkService = container.Resolve<VkService>();
                var logger = container.Resolve<Logger>();
                logger.Info("");
                logger.Info("");
                logger.Info("");
                logger.Info("");

                logger.Info("=====================================================================================");
                logger.Info("Starting MusicX App...");
                logger.Info("=====================================================================================");



                var navigationService = container.Resolve<NavigationService>();
                var configService = container.Resolve<ConfigService>();

                var config = await configService.GetConfig();

                logger.Info("Check new updater");
                if(File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\MusicX.UpdaterNew.exe"))
                {
                    logger.Info("Check old updater");

                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\MusicX.Updater.exe"))
                    {
                        logger.Info("Delete old updater");

                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\MusicX.Updater.exe");
                    }

                    logger.Info("Rename new updater");

                    File.Move(AppDomain.CurrentDomain.BaseDirectory + "\\MusicX.UpdaterNew.exe", AppDomain.CurrentDomain.BaseDirectory + "\\MusicX.Updater.exe");
                }

                
                await Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    if (config.AccessToken is null)
                    {
                        var login = new LoginWindow(vkService, configService, logger, navigationService);
                        login.Show();
                        this.Close();
                    }
                    else
                    {
                        await vkService.SetTokenAsync(config.AccessToken, null);
                        var rootWindow = new RootWindow(navigationService, vkService, logger, configService);
                        rootWindow.Show();
                        this.Close();
                    }
                });

                
            });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            


        }
    }
}
