﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Threading;

namespace FH2CommunityUpdater
{

    public partial class UpdateWindow : Form
    {
        public List<string> torrentFiles;
        
        internal ContentManager contentManager;
        internal TorrentUser torrentUser;
        public MainWindow parent;
        internal List<string> changeLogs = new List<string>();
        private int amount = 0;
        private int finishedDL = 0;


        protected internal DialogResult ShowDialog(IWin32Window owner, bool start)
        {
            Start();
            return this.ShowDialog(owner);
        }

        internal UpdateWindow(MainWindow parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.contentManager = parent.contentManager;
            this.torrentUser = parent.torrentUser;
        }

        public void Start()
        {
            this.contentManager.cancelAll(this);
            if (this.contentManager.Busy)
                this.contentManager.Released += contentManager_Released;
            else
                contentManager_Released(this.contentManager, new EventArgs());
        }

        void contentManager_Released(object o, EventArgs e)
        {
            this.contentManager.Released -= contentManager_Released;
            this.contentManager.MD5ProgressChanged += contentManager_MD5ProgressChanged;
            this.contentManager.MD5Completed += contentManager_MD5Completed;
            this.contentManager.findObsoleteFiles(this);
        }

        void contentManager_MD5ProgressChanged(object sender, MD5ProgressChangedEventArgs e)
        {
            setMD5Info(e.Progress);
        }
        void contentManager_MD5Completed(object sender, MD5ProgressChangedEventArgs e)
        {
            this.contentManager.MD5Completed -= contentManager_MD5Completed;
            this.contentManager.MD5ProgressChanged -= contentManager_MD5ProgressChanged;
            setMD5Info(e.Progress);

            this.contentManager.setNotBusy(this);
            Continue();
        }

        private void Continue()
        {
            if (!this.parent.updateInProgress)
                return;
            setProgressBar(0);
            List<FH2File> toDownload = this.contentManager.getObsoleteFiles(this);
            if (toDownload.Count == 0)
            {
                string message;
                if (this.contentManager.getSelectedAddons().Count == 1)
                    message = "Active addon is up to date.";
                else
                    message = "Active addons are up to date.";
                MessageBox.Show(message);
                disposeThis();
                return;
            }
            int i = 0;
            List<Uri[]> torrentURLs = new List<Uri[]>();
            foreach (ContentClass addon in this.contentManager.getOutdatedAddons())
            {
                Uri[] info = {addon.torrent, new Uri(
                    Path.Combine(this.parent.localAppDataFolder, addon.ID.ToString() + ".torrent"))};
                torrentURLs.Add(info);
                if (((addon.addonState == AddonState.UpdateAvailable)||(addon.addonState == AddonState.NotInstalled)) && (addon.hasChangelog))
                    changeLogs.Add(addon.changeLog);

            }
            amount = torrentURLs.Count;
            WebClient web = new WebClient();
            web.DownloadProgressChanged += web_DownloadProgressChanged;
            web.DownloadFileCompleted += new AsyncCompletedEventHandler(
            delegate(object o, AsyncCompletedEventArgs args)
            {
                if (args.Error != null)
                {
                    if (args.Error.GetType() == (typeof(WebException)))
                    {
                        MessageBox.Show("Could not connect to the server.\nA Please check your connections and/or try again later.\nProgram will shut down.");
                        Environment.Exit(4);
                    }
                    else
                        throw args.Error;
                }
                this.finishedDL += 100;
                if (i >= torrentURLs.Count)
                {
                    torrentDLFinished(torrentURLs);
                    web.Dispose();
                }
                else
                {
                    Console.WriteLine(torrentURLs[i][0]);
                    Console.WriteLine(torrentURLs[i][1]);
                    web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
                    setTorrentDLInfo(i, torrentURLs.Count);
                    i++;
                }
            });
            if (i != 0)
                return;
            Console.WriteLine(torrentURLs[i][0]);
            Console.WriteLine(torrentURLs[i][1]);
            web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
            setTorrentDLInfo(i, torrentURLs.Count);
            i++;
        }

        void web_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine(e.ProgressPercentage);
            int progress = 100*this.finishedDL + 100*e.ProgressPercentage;
            setProgressBar(progress);
            
        }

        private void torrentDLFinished( List<Uri[]> torrentURLs )
        {
            if (!this.parent.updateInProgress)
                return;
            setProgressBar(10000);
            int total = torrentURLs.Count;
            string info = "Downloading Torrent Files ( " + total.ToString() + " of " + total.ToString() + " completed. )";
            setInfoLabel(info);
            List<string> torrentPaths = new List<string>();
            foreach (Uri[] entry in torrentURLs)
            {
                torrentPaths.Add(entry[1].OriginalString);
            }
            if (this.parent.debugMode)
                this.torrentUser.debugWindow.Show();
            this.torrentUser.StatusUpdate += torrentUser_StatusUpdate;
            this.torrentUser.TorrentDownloadCompleted += torrentUser_TorrentDownloadCompleted;
            this.torrentUser.LoadTorrents(torrentPaths, this.contentManager.getObsoleteFiles(this));
            this.button1.Enabled = true;
        }

        void torrentUser_TorrentDownloadCompleted(object sender, TorrentStatusUpdateEventArgs e)
        {
            setTorrentInfo(e);
            if (!this.parent.updateInProgress)
                return;
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (this.changeLogs.Count == 0)
                        MessageBox.Show(this, "Update Completed.", "Success");
                    else
                    {
                        string message = "Update Completed. Would you like to see\nthe most recent changelogs?";
                        string caption = "Success";
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show(message, caption, buttons);
                        if (result == System.Windows.Forms.DialogResult.Yes)
                            foreach (string changelog in this.changeLogs)
                            {
                                DirectoryInfo folder = new DirectoryInfo(Application.StartupPath);
                                folder = folder.Parent.Parent;
                                string adress = Path.Combine(folder.FullName, changelog);
                                try
                                {
                                    System.Diagnostics.Process.Start("notepad.exe", adress);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }

                    }
                });
            }
            catch (Exception)
            {

            }
            this.torrentUser.StatusUpdate -= torrentUser_StatusUpdate;
            this.torrentUser.TorrentDownloadCompleted -= torrentUser_TorrentDownloadCompleted;
            this.parent.updateInProgress = false;
            disposeThis();
            return;
        }

        private void disposeThis()
        {
            if (this.InvokeRequired)
            {
                disposeThisCallback d = new disposeThisCallback(disposeThis);
                this.Invoke(d, new object[] {});
            }
            else
            {
                //this.parent.BringToFront();
                this.Dispose();
            }
        }
        delegate void disposeThisCallback();

        void torrentUser_StatusUpdate(object sender, TorrentStatusUpdateEventArgs e)
        {
            setTorrentInfo(e);
        }

        private void setTorrentInfo(TorrentStatusUpdateEventArgs e)
        {
            if (this.progressBar1.InvokeRequired)
            {
                setTorrentInfoCallback d = new setTorrentInfoCallback(setTorrentInfo);
                this.Invoke(d, new object[] { e });
            }
            else
            {
            setProgressBar((int)(e.Progress * 100));
            setInfoLabel(e.infoMessage);
            setTimeLabel(e.timeMessage);
            }
        }
        delegate void setTorrentInfoCallback(TorrentStatusUpdateEventArgs e);

        private void setTorrentDLInfo(int i, int total)
        {
            int progress = (int)(((double)i / (double)total)*10000);
            if ((progress > this.progressBar1.Maximum) || (progress < this.progressBar1.Minimum))
                return;
            setProgressBar(progress);
            string info = "Downloading Torrent Files ( " + i.ToString() + " of " + total.ToString() + " completed. )";
            setInfoLabel(info);
        }

        private void setMD5Info(double progress)
        {
            setProgressBar((int)(progress * 100));
            string info = "Checking local files (" + ((int)(progress)).ToString() + "% complete.)";
            setInfoLabel(info);
        }

        private void setProgressBar(int progress)
        {
            if (this.progressBar1.InvokeRequired)
            {
                setProgressBarCallback d = new setProgressBarCallback(setProgressBar);
                try { this.Invoke(d, new object[] { progress }); }
                catch { }
            }
            else
            {
                this.progressBar1.Maximum = 10000;
                try
                {
                    if ((progress <= this.progressBar1.Maximum) || (progress >= this.progressBar1.Minimum))
                        this.progressBar1.Value = progress;
                }
                catch
                {

                }
            }
        }
        delegate void setProgressBarCallback(int progress);

        private void setInfoLabel(string text)
        {
            if (this.label1.InvokeRequired)
            {
                setInfoLabelCallback d = new setInfoLabelCallback(setInfoLabel);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.label1.Text = text;
            }
        }
        delegate void setInfoLabelCallback(string text);

        private void setTimeLabel(string text)
        {
            if (this.label2.InvokeRequired)
            {
                setTimeLabelCallback d = new setTimeLabelCallback(setTimeLabel);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.label2.Text = text;
            }
        }
        delegate void setTimeLabelCallback(string text);


        private void button1_Click(object sender, EventArgs e)
        {
            if (this.button1.Text == "Pause")
            {
                this.label1.Text = "Download Paused.";
                this.torrentUser.engine.PauseAll();
                this.button1.Text = "Resume";
                this.torrentUser.engineState = EngineState.Paused;
            }
            else
            {
                this.torrentUser.engine.StartAll();
                this.button1.Text = "Pause";
                this.torrentUser.engineState = EngineState.Downloading;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string message = "Are you sure you want to cancel this update?\nAddons might become unplayable until the update is completed.";
            string caption = "Confirm";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult dresult;
            dresult = MessageBox.Show(message, caption, buttons);
            if (dresult == System.Windows.Forms.DialogResult.No)
                return;
            this.parent.updateInProgress = false;
            this.torrentUser.StopUpdate();
            this.disposeThis();
        }         
    }
}
