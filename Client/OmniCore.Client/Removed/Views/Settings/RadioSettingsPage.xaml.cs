﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniCore.Client.ViewModels.Settings;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace OmniCore.Client.Views.Settings
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class RadioSettingsPage : ContentPage
    {
        public RadioSettingsPage()
        {
            InitializeComponent();
            new RadioSettingsViewModel(this);
        }
    }
}