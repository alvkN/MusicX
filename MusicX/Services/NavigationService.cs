﻿using MusicX.Core.Models;
using MusicX.Models.Enums;
using MusicX.Views;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MusicX.Services
{
    public class NavigationService
    {
        private bool nowModalOpen;
        public event OpenModalDelegate OpenedModalWindow;
        public event CloseModalDelegate ClosedModalWindow;

        public delegate void OpenModalDelegate(object Page, int height, int width);
        public delegate void CloseModalDelegate();

        private readonly Logger logger;
        private RootWindow rootWindow;

        public SectionView SectionView { get; set; }
        public Frame CurrentFrame { get; set; }

        public Stack<(NavigationSource Source, object Data)> History { get; set; } = new Stack<(NavigationSource Source, object Data)> ();

        public NavigationService(Logger logger)
        {
            this.logger = logger;
        }
        public void NavigateToPage(object page, bool fromHistory = false)
        {
            logger.Info($"Navigate to {page.GetType} page, from history = {fromHistory}");
            if (!fromHistory) AddHistory(NavigationSource.Page, page);

            CurrentFrame.Navigate(page);
        }


        public void OpenModal(object page, int height, int width)
        {
            nowModalOpen = true;
            OpenedModalWindow?.Invoke(page, height, width);
        }

        public void CloseModal()
        {
            if (!nowModalOpen) return;

            ClosedModalWindow?.Invoke();

            nowModalOpen = false;
        }

        public async Task OpenSection(string sectionId, bool showTitle = false)
        {
            logger.Info($"Open section {sectionId}");

            if (History.First().Source == NavigationSource.Page) CurrentFrame.Navigate(SectionView);
            await SectionView.LoadSection(sectionId, showTitle);
        }

        public async Task OpenArtistSection(string artistId)
        {
            logger.Info($"Open artist section with artistId = {artistId}");
            if (History.First().Source == NavigationSource.Page)
            {
                CurrentFrame.Navigate(SectionView);
            }
            await SectionView.LoadArtistSection(artistId);
        }

        public async Task OpenSearchSection(string query)
        {
            logger.Info($"Open sarch section with query = {query}");
            if (History.First().Source == NavigationSource.Page) CurrentFrame.Navigate(SectionView);
            await SectionView.LoadSearchSection(query);
        }

        public async Task OpenSectionByBlocks(List<Block> blocks, string next)
        {
            logger.Info($"Open section by {blocks.Count} blocks");
            if (History.First().Source == NavigationSource.Page) CurrentFrame.Navigate(SectionView);
            await SectionView.SetBlocks(blocks, next);
        }

        public void AddHistory(NavigationSource source, object data)
        {
            logger.Info($"Add {source} to history with data {data.GetType}");

            History.Push((source, data));
        }

        public async Task ReplaceBlock(string replace_id)
        {
            logger.Info($"Replace block with replace id = {replace_id}");
            await SectionView.ReplaceBlocks(replace_id);
        }

        public async Task Back()
        {
            logger.Info($"Go to back");

            if (History.Count < 2) return;

            History.Pop();

            var history = History.Pop();

            if(history.Source == NavigationSource.Section)
            {
                var blocks = ((List<Block> blocks, string next)) history.Data;

                NavigateToPage(SectionView);
                await OpenSectionByBlocks(blocks.blocks, blocks.next);
            }else if(history.Source == NavigationSource.Page)
            {
                NavigateToPage(history.Data, true);
            }
        }

        public void SetRootWindow(RootWindow rootWindow)
        {
            this.rootWindow = rootWindow;
        } 

        public void CloseRootWindow()
        {
            rootWindow.Close();
        }
    }
}
