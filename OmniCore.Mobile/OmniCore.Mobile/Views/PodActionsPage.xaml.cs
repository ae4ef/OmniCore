﻿using OmniCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace OmniCore.Mobile.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PodActionsPage : ContentPage
    {
        public PodActionsPage()
        {
            InitializeComponent();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            var podManager = App.Instance.PodProvider.PodManager;
            using(var conversation = await podManager.StartConversation())
            {
                await podManager.Bolus(conversation, 1.0m);
            }
            
        }

        private async void Button_Clicked_1(object sender, EventArgs e)
        {
            var podManager = App.Instance.PodProvider.PodManager;
            using (var conversation = await podManager.StartConversation())
            {
                await podManager.SetTempBasal(conversation, 30m, 0.5m);
            }
        }

        private async void Button_Clicked_2(object sender, EventArgs e)
        {
            var podManager = App.Instance.PodProvider.PodManager;
            using (var conversation = await podManager.StartConversation())
            {
                await podManager.CancelTempBasal(conversation);
            }
        }

        private async void Button_Clicked_3(object sender, EventArgs e)
        {
            var podManager = App.Instance.PodProvider.PodManager;
            using (var conversation = await podManager.StartConversation())
            {
                var schedule = new decimal[48];
                for (int i = 0; i < 48; i++)
                    schedule[i] = 8.85m;

                await podManager.SetBasalSchedule(conversation, schedule, 60);
            }
        }
    }
}