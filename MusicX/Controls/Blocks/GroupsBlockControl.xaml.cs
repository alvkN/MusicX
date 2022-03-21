﻿using DryIoc;
using MusicX.Core.Models;
using MusicX.Services;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicX.Controls.Blocks
{
    /// <summary>
    /// Логика взаимодействия для GroupsBlockControl.xaml
    /// </summary>
    public partial class GroupsBlockControl : UserControl
    {
        public Block Block { get; set; }
        public GroupsBlockControl()
        {
            InitializeComponent();
            this.Loaded += GroupsBlockControl_Loaded;
        }

        private void GroupsBlockControl_Loaded(object sender, RoutedEventArgs e)
        {
            if(Block.Groups[0].Photo100 != null) GroupImage.ImageSource = new BitmapImage(new Uri(Block.Groups[0].Photo100));

            GroupName.Text = Block.Groups[0].Name;
            GroupSub.Text = Block.Groups[0].MembersCount.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://vk.com/" + Block.Groups[0].ScreenName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                var logger = StaticService.Container.Resolve<Logger>();

                logger.Error(ex, ex.Message);
            }
        }
    }
}
