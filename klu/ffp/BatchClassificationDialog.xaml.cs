﻿using System;
using System.Collections;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ffp.TrainingDataSetTableAdapters;
using KluSharp;
using Microsoft.Windows.Controls.Ribbon;

namespace ffp
{
    /// <summary>
    /// Interaction logic for BatchClassificationDialog.xaml
    /// </summary>
    public partial class BatchClassificationDialog : Window
    {
        TrainingDataSet _DataSet;
        ArrayList _SelectedFiles;
        FaceFeaturePoints _FFP;
        ProcessOptions _ProcessOptions;
        Klu _KLU;
        System.Drawing.Bitmap _TempBitmap;
        BackgroundWorker _BackgroundWorker;


        public BatchClassificationDialog(ref Klu klu, ref TrainingDataSet dataSet, ProcessOptions processOptions)
        {
            InitializeComponent();

            _BackgroundWorker = new BackgroundWorker();
            _BackgroundWorker.WorkerReportsProgress = true;
            _BackgroundWorker.WorkerSupportsCancellation = true;
            _BackgroundWorker.DoWork += new DoWorkEventHandler(DoWork);
            _BackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunWorkerCompleted);
            _BackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);        

            BrowseButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            ClassifyButton.IsEnabled = false;

            _DataSet = dataSet;
            _SelectedFiles = new ArrayList();
            _ProcessOptions = processOptions;
            // We want the images to be as less distorded as possible, so we disable all the overlays.

            _ProcessOptions.DrawAnthropometricPoints = 0;
            _ProcessOptions.DrawDetectionTime = 0;
            _ProcessOptions.DrawFaceRectangle = 0;
            _ProcessOptions.DrawSearchRectangles = 0;
            _ProcessOptions.DrawFeaturePoints = 0;
            
            _KLU = klu;
            _FFP = new FaceFeaturePoints();
            _TempBitmap = new System.Drawing.Bitmap(10, 10);

            ExpressionsComboBox.ItemsSource = _DataSet.Expression;
            ClassifyButton.Content = "Classify";
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            int expressionOID = Convert.ToInt32(e.Argument);

            // Process all the selected images files in another thread. 

            for (int i = 0; i < _SelectedFiles.Count; i++)
            {
                _KLU.ProcessStillImage((string)_SelectedFiles[i], ref _ProcessOptions, ref _FFP);

                int width = 0, height = 0;
                _KLU.GetLastProcessedImageDims(ref width, ref height);

                if (_TempBitmap.Width != width || _TempBitmap.Height != height)
                {
                    Console.WriteLine("Need to resize the _TempBitmap to " + width + "x" + height);
                    _TempBitmap.Dispose();
                    GC.Collect();
                    _TempBitmap = new System.Drawing.Bitmap(width, height);
                }

                _KLU.GetLastProcessedImage(ref _TempBitmap);

                // Add the training data to the dataset
                ffp.MainWindow.AddTrainingData(ref _DataSet, ref _FFP, expressionOID, ref _TempBitmap);

                if (i % 10 == 0 || i == (_SelectedFiles.Count - 1))
                {
                    worker.ReportProgress((int)(100*(float)(i + 1) / (float)_SelectedFiles.Count));
                }
            }
        }

        private void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BrowseButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            ClassifyButton.IsEnabled = false;
        }

        private void ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure save file dialog box            
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = "."; // Default file extension
            dlg.Filter = "Imagefiles (*.bmp, *.jpg, *.png, *.tif, *.tiff, *.tga, *.pgm)|*.bmp;*.jpg;*.png;*.tif;*.tiff;*.tga;*.pgm"; // Filter files by extension
            dlg.Title = "Load images";
            dlg.Multiselect = true;

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                _SelectedFiles.Clear();
                _SelectedFiles.AddRange(dlg.FileNames);

                if (dlg.FileNames.GetLength(0) > 0) 
                {
                    ClassifyButton.IsEnabled = true;
                }
            }

            ClassifyButton.Content = "Classify " + _SelectedFiles.Count + " file(s)";
        }

        private void ClassifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_SelectedFiles.Count < 1)
            {
                MessageBox.Show("You must at least pick 1 file to classify. Use the \"Browse\" button to look for image files.", "Request for file selection", MessageBoxButton.OK);
                return;
            }

            // Setup the progress bar
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Maximum = 100;
            ProgressBar.Minimum = 0;
            ProgressBar.Value = 0;

            BrowseButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ClassifyButton.IsEnabled = false;

            _BackgroundWorker.RunWorkerAsync(Convert.ToUInt32(ExpressionsComboBox.SelectedValue));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _BackgroundWorker.CancelAsync();

            BrowseButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            ClassifyButton.IsEnabled = false;
        }
    }
}
