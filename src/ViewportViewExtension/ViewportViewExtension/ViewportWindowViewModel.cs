﻿using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Interfaces;
using Dynamo.Core;
using Dynamo.Extensions;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.Visualization;
using CefSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ViewportViewExtension
{
   public class ViewportWindowViewModel : NotificationObject, IDisposable
    {
        private string selectedNodesText = "Begin selecting ";

        // Variable for storing a reference to our loaded parameters
        private ReadyParams readyParams;
        
        public RenderPackageCache PackageContent { get; set; }
        public string TransactionType { get; set; }
        public string NodeGuid { get; set;  }
        public bool DisplayPreview { get; set; } = true; // C#6.0

        // Find render nodes and build THREE meshes
        public string RenderData => $"{getRenderPackages()}"; // C#6.0

        public string getRenderPackages()
        {
            // javascript function
            string output = "renderDynamoMesh(";

            // TODO don't convert enums to lists?
            List<double[]> verts = new List<double[]>();
            List<double[]> normals = new List<double[]>();
            //List<List<int>> vertIndices = new List<List<int>>();
            List<List<double>> points = new List<List<double>>();
            List<List<double>> lines = new List<List<double>>();

            foreach (IRenderPackage p in PackageContent.Packages)
            {
                verts.Add(p.MeshVertices.ToArray());
                //vertIndices.Add(p.MeshIndices.ToList());
                normals.Add(p.MeshNormals.ToArray());
                points.Add(p.PointVertices.ToList());
                lines.Add(p.LineStripVertices.ToList());
            }

            Dictionary<string, Object> groupData = new Dictionary<string, object>();
            groupData.Add("name", NodeGuid);
            groupData.Add("transactionType", TransactionType);
            groupData.Add("displayPreview", DisplayPreview);
            groupData.Add("vertices", verts);
            groupData.Add("normals", normals);
            //groupData.Add("faceIndices", vertIndices);
            groupData.Add("points", points);
            groupData.Add("lines", lines);

            string jsonString = JsonConvert.SerializeObject(groupData/*, Formatting.Indented*/);

            output += jsonString + ");";

            return output;
        }

        public ViewportWindowViewModel(ReadyParams p)
        {
            // Save a reference to our loaded parameters which
            // is required in order to access the workspaces
            readyParams = p;

            // Subscribe to NodeAdded and NodeRemoved events
            p.CurrentWorkspaceModel.NodeAdded += CurrentWorkspaceModel_NodeAdded;
            p.CurrentWorkspaceModel.NodeRemoved += CurrentWorkspaceModel_NodeRemoved;

            // TODO this could be dangerous if called in custom node ws
            HomeWorkspaceModel currentWS = readyParams.CurrentWorkspaceModel as HomeWorkspaceModel;
            //currentWS.RefreshCompleted += CurrentWorkspaceModel_NodesChanged;
        }

        // When a new node is added to the workspace
        private void CurrentWorkspaceModel_NodeAdded(NodeModel node)
        {
            // Update viewport when nodes render package is updated
            node.RenderPackagesUpdated += CurrentWorkspaceModel_UpdateViewportGeometry;
            
            // Listen for property changes on added node ex: toggle background preview
            node.PropertyChanged += CurrentWorkspaceModel_nodePropertyChanged;
        }

        // When an existing node is removed from the workspace
        private void CurrentWorkspaceModel_NodeRemoved(NodeModel node)
        {
            NodeGuid = node.GUID.ToString();
            TransactionType = "remove";
            // Unregister when node is removed
            RaisePropertyChanged("RenderData");
            node.RenderPackagesUpdated -= CurrentWorkspaceModel_UpdateViewportGeometry;
        }

        private void CurrentWorkspaceModel_UpdateViewportGeometry(NodeModel nodeModel, RenderPackageCache packages)
        {
            PackageContent = packages;
            DisplayPreview = nodeModel.ShouldDisplayPreview;
            NodeGuid = nodeModel.GUID.ToString();
            TransactionType = "update";
            RaisePropertyChanged("RenderData");
        }

        private void CurrentWorkspaceModel_nodePropertyChanged(object sender, EventArgs e)
        {
            NodeModel node = sender as NodeModel;
            PropertyChangedEventArgs eventArgs= e as PropertyChangedEventArgs;
            string changedProperty = eventArgs.PropertyName;

            if(changedProperty == "IsVisible")
            {
                TransactionType = "togglePreview";
                NodeGuid = node.GUID.ToString();
                DisplayPreview = node.IsVisible;
                RaisePropertyChanged("RenderData");
            }
        }

        // Very important - unsubscribe from our events
        public void Dispose()
        {          
            readyParams.CurrentWorkspaceModel.NodeAdded -= CurrentWorkspaceModel_NodeAdded;
            readyParams.CurrentWorkspaceModel.NodeRemoved -= CurrentWorkspaceModel_NodeRemoved;
        }
    }
}
