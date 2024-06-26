﻿using System.Windows;
using System.Windows.Controls;

namespace MusicX.Controls
{
    /// <summary>
    /// Логика взаимодействия для ListPlaylists.xaml
    /// </summary>
    public partial class ListPlaylists : UserControl
    {

        public ListPlaylists()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ShowFullProperty = DependencyProperty.Register(
            nameof(ShowFull), typeof(bool), typeof(ListPlaylists));

        public bool ShowFull
        {
            get => (bool)GetValue(ShowFullProperty);
            set => SetValue(ShowFullProperty, value);
        }

        public static readonly DependencyProperty IsCroppedProperty = DependencyProperty.Register(
           nameof(IsCropped), typeof(bool), typeof(ListPlaylists));

        public bool IsCropped
        {
            get => (bool)GetValue(IsCroppedProperty);
            set => SetValue(IsCroppedProperty, value);
        }
    }
}
