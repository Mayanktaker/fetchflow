// © Mayanktaker Computers & Web Development | https://mayanktaker.com

using System;
using System.Collections.Generic;
using Gtk;
using XDM.Core;

namespace XDM.GtkUI.Controls
{
    // Responsive container holding modern DownloadCardWidget components
    public class DownloadCardContainer : ScrolledWindow
    {
        private readonly VBox cardsVBox;
        private readonly Dictionary<string, DownloadCardWidget> activeCards = new();
        private readonly Label emptyStateLabel;

        public event EventHandler<InProgressDownloadItem>? PauseRequested;
        public event EventHandler<InProgressDownloadItem>? ResumeRequested;
        public event EventHandler<InProgressDownloadItem>? DeleteRequested;
        public event EventHandler<InProgressDownloadItem>? OpenFolderRequested;

        public DownloadCardContainer()
        {
            OverlayScrolling = true;
            ShadowType = ShadowType.None;
            HscrollbarPolicy = PolicyType.Never;
            VscrollbarPolicy = PolicyType.Automatic;
            StyleContext.AddClass("card-container-scroll");

            cardsVBox = new VBox(false, 2)
            {
                MarginStart = 4,
                MarginEnd = 4,
                MarginTop = 4,
                MarginBottom = 4
            };

            emptyStateLabel = new Label
            {
                Text = "No active downloads in queue\nClick '+ Add URL' above to start downloading",
                Justify = Justification.Center,
                MarginTop = 40,
                MarginBottom = 40
            };
            emptyStateLabel.StyleContext.AddClass("empty-state-label");
            cardsVBox.PackStart(emptyStateLabel, true, true, 0);

            Add(cardsVBox);
            ShowAll();
        }

        // Sets or replaces all in-progress downloads in the card view
        public void SetDownloads(IEnumerable<InProgressDownloadItem> items)
        {
            // Clear existing cards
            foreach (var card in activeCards.Values)
            {
                cardsVBox.Remove(card);
                card.Destroy();
            }
            activeCards.Clear();

            int count = 0;
            foreach (var item in items)
            {
                AddOrUpdateCard(item);
                count++;
            }

            emptyStateLabel.Visible = (count == 0);
            cardsVBox.ShowAll();
        }

        // Adds or updates a single download item card
        public void AddOrUpdateCard(InProgressDownloadItem item)
        {
            if (activeCards.TryGetValue(item.Id, out var existingCard))
            {
                existingCard.SetInProgress(item);
            }
            else
            {
                var card = new DownloadCardWidget(item);
                card.PauseResumeClicked += (s, e) =>
                {
                    if (card.InProgressItem != null)
                    {
                        if (card.InProgressItem.Status == DownloadStatus.Downloading)
                            PauseRequested?.Invoke(this, card.InProgressItem);
                        else
                            ResumeRequested?.Invoke(this, card.InProgressItem);
                    }
                };
                card.DeleteClicked += (s, e) =>
                {
                    if (card.InProgressItem != null)
                        DeleteRequested?.Invoke(this, card.InProgressItem);
                };
                card.OpenFolderClicked += (s, e) =>
                {
                    if (card.InProgressItem != null)
                        OpenFolderRequested?.Invoke(this, card.InProgressItem);
                };

                activeCards[item.Id] = card;
                // Insert before emptyStateLabel
                cardsVBox.PackStart(card, false, false, 0);
                card.ShowAll();
            }

            emptyStateLabel.Visible = (activeCards.Count == 0);
        }

        // Removes a finished or deleted download item card
        public void RemoveCard(string id)
        {
            if (activeCards.TryGetValue(id, out var card))
            {
                cardsVBox.Remove(card);
                card.Destroy();
                activeCards.Remove(id);
            }
            emptyStateLabel.Visible = (activeCards.Count == 0);
        }

        // Updates metrics on a specific active card
        public void UpdateCardProgress(string id, int progress, string? speed, string? eta, DownloadStatus status)
        {
            if (activeCards.TryGetValue(id, out var card))
            {
                card.UpdateProgress(progress, speed, eta, status);
            }
        }
    }
}
