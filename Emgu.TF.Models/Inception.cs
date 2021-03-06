﻿//----------------------------------------------------------------------------
//  Copyright (C) 2004-2018 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Emgu.TF;
using Emgu.Models;
using System.IO;
using System.ComponentModel;
using System.Net;

namespace Emgu.TF.Models
{
    public class Inception : Emgu.TF.Util.UnmanagedObject
    {
        private FileDownloadManager _downloadManager;
        private Graph _graph = null;
        private SessionOptions _sessionOptions = null;
        private Session _session = null;
        private Status _status = null;
        private String _inputName = null;
        private String _outputName = null;
        private String[] _labels = null;

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
        public double DownloadProgress
        {
            get
            {
                if (_downloadManager == null)
                    return 0;
                if (_downloadManager.CurrentWebClient == null)
                    return 1;
                return _downloadManager.CurrentWebClient.downloadProgress;
            }
        }

        public String DownloadFileName
        {
            get
            {
                if (_downloadManager == null)
                    return null;
                if (_downloadManager.CurrentWebClient == null)
                    return null;
                return _downloadManager.CurrentWebClient.url;
            }
        }
#endif

        public Inception(Status status = null, SessionOptions sessionOptions = null)
        {
            _status = status;
            _sessionOptions = sessionOptions;
            _downloadManager = new FileDownloadManager();

            _downloadManager.OnDownloadProgressChanged += onDownloadProgressChanged;
            _downloadManager.OnDownloadCompleted += onDownloadCompleted;
        }

        private void onDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ImportGraph();
            if (OnDownloadCompleted != null)
            {
                OnDownloadCompleted(sender, e);
            }
        }

        private void onDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (OnDownloadProgressChanged != null)
                OnDownloadProgressChanged(sender, e);
        }

        public event System.Net.DownloadProgressChangedEventHandler OnDownloadProgressChanged;
        public event System.ComponentModel.AsyncCompletedEventHandler OnDownloadCompleted;


        public
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            IEnumerator
#else
            void
#endif
            Init(String[] modelFiles = null, String downloadUrl = null, String inputName = null, String outputName = null)
        {
            _inputName = inputName == null ? "input" : inputName;
            _outputName = outputName == null ? "output" : outputName;

            _downloadManager.Clear();
            String url = downloadUrl == null ? "https://github.com/emgucv/models/raw/master/inception/" : downloadUrl;
            String[] fileNames = modelFiles == null ? new string[] { "tensorflow_inception_graph.pb", "imagenet_comp_graph_label_strings.txt" } : modelFiles;
            for (int i = 0; i < fileNames.Length; i++)
                _downloadManager.AddFile(url + fileNames[i]);

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            yield return _downloadManager.Download();
#else
            _downloadManager.Download();
#endif
        }

        public bool Imported
        {
            get
            {
                return _graph != null;
            }
        }

        private void ImportGraph()
        {
            if (_graph != null)
                _graph.Dispose();

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            UnityEngine.Debug.Log("Reading model definition");
#endif
            _graph = new Graph();
            String localFileName = _downloadManager.Files[0].LocalFile;
            byte[] model = File.ReadAllBytes(localFileName);
            if (model.Length == 0)
                throw new FileNotFoundException(String.Format("Unable to load file {0}", localFileName));
            Buffer modelBuffer = Buffer.FromString(model);

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            UnityEngine.Debug.Log("Importing model");
#endif
            using (ImportGraphDefOptions options = new ImportGraphDefOptions())
                _graph.ImportGraphDef(modelBuffer, options, _status);

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE
            UnityEngine.Debug.Log("Model imported");
#endif
            _session = new Session(_graph, _sessionOptions, _status);
            _labels = File.ReadAllLines(_downloadManager.Files[1].LocalFile);
        }

        public Graph Graph
        {
            get
            {
                return _graph;
            }
        }

        public String[] Labels
        {
            get { return _labels; }
        }

        public float[] Recognize(Tensor image)
        {
            Tensor[] finalTensor = _session.Run(new Output[] { _graph[_inputName] }, new Tensor[] { image },
                new Output[] { _graph[_outputName] });
            float[] probability = finalTensor[0].GetData(false) as float[];
            return probability;
        }

        public RecognitionResult MostLikely(Tensor image)
        {
            float[] probability = Recognize(image);

            RecognitionResult result = new RecognitionResult();
            result.Label = String.Empty;

            if (probability != null)
            {
                float maxVal = 0;
                int maxIdx = 0;
                for (int i = 0; i < probability.Length; i++)
                {
                    if (probability[i] > maxVal)
                    {
                        maxVal = probability[i];
                        maxIdx = i;
                    }
                }
                result.Label = _labels[maxIdx];
                result.Probability = maxVal;
            }
            return result;
        }

        public class RecognitionResult
        {
            public String Label;
            public double Probability;
        }

        protected override void DisposeObject()
        {
            if (_graph != null)
            {
                _graph.Dispose();
                _graph = null;
            }

            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }
        }
    }
}
