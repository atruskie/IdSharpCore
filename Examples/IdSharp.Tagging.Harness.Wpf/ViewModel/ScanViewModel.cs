﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using IdSharp.Tagging.Harness.Wpf.Commands;
using IdSharp.Tagging.Harness.Wpf.Model;
using IdSharp.Tagging.Harness.Wpf.ViewModel.Interfaces;
using IdSharp.Tagging.ID3v2;
using Application = System.Windows.Application;

namespace IdSharp.Tagging.Harness.Wpf.ViewModel
{
    public class ScanViewModel : ViewModelBase, IScanViewModel
    {
        private readonly FolderBrowserDialog _folderBrowserDialog;

        private readonly DelegateCommand _scanCommand;
        private readonly DelegateCommand _cancelCommand;
        private readonly DelegateCommand _browseCommand;

        private string _directory;
        private bool _isScanning;
        private int _percentComplete;
        private bool _cancelScanning;
        private ObservableCollection<Track> _trackCollection;

        public ScanViewModel()
        {
            PropertyChanged += ScanViewModel_PropertyChanged;

            _scanCommand = new DelegateCommand(Scan, () => !IsScanning && System.IO.Directory.Exists(Directory));
            _cancelCommand = new DelegateCommand(CancelScan, () => IsScanning && !_cancelScanning);
            _browseCommand = new DelegateCommand(Browse, () => !IsScanning);

            _folderBrowserDialog = new FolderBrowserDialog();
        }

        private void ScanViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsScanning")
            {
                _scanCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
                _browseCommand.RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == "Directory")
            {
                _scanCommand.RaiseCanExecuteChanged();
            }
        }

        public string Directory
        {
            get { return _directory; }
            set
            {
                if (_directory != value)
                {
                    _directory = value;
                    SendPropertyChanged("Directory");
                }
            }
        }

        public bool IsScanning
        {
            get { return _isScanning; }
            private set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    SendPropertyChanged("IsScanning");
                }
            }
        }

        public ICommand ScanCommand
        {
            get { return _scanCommand; }
        }

        public ICommand CancelCommand
        {
            get { return _cancelCommand; }
        }

        public ICommand BrowseCommand
        {
            get { return _browseCommand; }
        }

        public int PercentComplete
        {
            get { return _percentComplete; }
            private set
            {
                if (_percentComplete != value)
                {
                    _percentComplete = value;
                    SendPropertyChanged("PercentComplete");
                }
            }
        }

        public ObservableCollection<Track> TrackCollection
        {
            get { return _trackCollection; }
            private set
            {
                if (_trackCollection != value)
                {
                    _trackCollection = value;
                    SendPropertyChanged("TrackCollection");
                }
            }
        }

        private void Scan()
        {
            string basePath = Directory;

            if (!System.IO.Directory.Exists(basePath))
                return;

            PercentComplete = 0;
            IsScanning = true;
            _cancelScanning = false;

            ThreadPool.QueueUserWorkItem(ScanDirectory, basePath);
        }

        private void CancelScan()
        {
            _cancelScanning = true;
            _cancelCommand.RaiseCanExecuteChanged();
        }

        private void Browse()
        {
            if (_folderBrowserDialog.ShowDialog(Application.Current.MainWindow.GetIWin32Window()) == DialogResult.OK)
            {
                Directory = _folderBrowserDialog.SelectedPath;
            }
        }

        private void ScanDirectory(object basePathObject)
        {
            int totalFiles = 0;
            ObservableCollection<Track> trackList = new ObservableCollection<Track>();
            try
            {
                string basePath = (string)basePathObject;

                DirectoryInfo di = new DirectoryInfo(basePath);
                FileInfo[] fileList = di.GetFiles("*.mp3", SearchOption.AllDirectories);

                totalFiles = fileList.Length;

                for (int i = 0; i < totalFiles; i++)
                {
                    if (_cancelScanning)
                    {
                        totalFiles = i;
                        break;
                    }

                    IID3v2Tag id3 = new ID3v2Tag(fileList[i].FullName);

                    trackList.Add(new Track(id3.Artist, id3.Title, id3.Album, id3.Year, id3.Genre, fileList[i].Name));

                    int percent = i * 100 / totalFiles;
                    if (percent > PercentComplete)
                    {
                        UpdateProgress(percent);
                    }
                }

                if (!_cancelScanning)
                {
                    UpdateProgress(100);
                }
            }
            finally
            {
                EndRecursiveScanning(totalFiles, trackList);
            }
        }

        private void EndRecursiveScanning(int totalFiles, ObservableCollection<Track> trackList)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action<int, ObservableCollection<Track>>(EndRecursiveScanning), totalFiles, trackList);
                return;
            }

            TrackCollection = trackList;
            IsScanning = false;
        }

        private void UpdateProgress(int percent)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action<int>(UpdateProgress), percent);
                return;
            }

            PercentComplete = percent;
        }
    }
}