﻿using System;
using System.Collections.Generic;
using StudioSB.Scenes;
using System.Windows.Forms;
using StudioSB.GUI.Attachments;

namespace StudioSB.GUI
{
    public class SBBoneTree : SBTreeView, IAttachment
    {
        private SBBoneEditor BoneEditor { get; set; }

        public SBBoneTree() : base()
        {
            //CheckBoxes = true;
            //ImageList = new System.Windows.Forms.ImageList();
            //ImageList.ImageSize = new System.Drawing.Size(24, 24);

            Indent = 8;

            LabelEdit = true;

            AfterSelect += SelectBone;

            AfterLabelEdit += treeView_AfterLabelEdit;
            
            Dock = DockStyle.Top;

            Size = new System.Drawing.Size(400, 400);

            BoneEditor = new SBBoneEditor();
            BoneEditor.Dock = DockStyle.Fill;
        }

        private void treeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            this.BeginInvoke(new Action(() => afterAfterEdit(e.Node)));
        }

        private void afterAfterEdit(TreeNode node)
        {
            if (node.Tag is SBBone bone)
                bone.Name = node.Text;
        }

        /// <summary>
        /// Selects a bone and displays information
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SelectBone(object sender, EventArgs args)
        {
            if (SelectedNode == null)
            {
                BoneEditor.Visible = false;
                return;
            }

            BoneEditor.BindBone((SBBone)SelectedNode.Tag);
            BoneEditor.Visible = true;
        }

        /// <summary>
        /// loads the bone nodes from a scene
        /// </summary>
        /// <param name="Scene"></param>
        private void LoadFromScene(SBScene Scene)
        {
            Nodes.Clear();
            Dictionary<SBBone, SBTreeNode> boneToNode = new Dictionary<SBBone, SBTreeNode>();
            if(Scene.Skeleton != null)
            foreach(var bone in Scene.Skeleton.Bones)
            {
                var node = new SBTreeNode(bone.Name) { Tag = bone };
                boneToNode.Add(bone, node);
                if (bone.Parent == null)
                    Nodes.Add(node);
                else if (boneToNode.ContainsKey(bone.Parent))
                    boneToNode[bone.Parent].Nodes.Add(node);
            }
            if(Nodes.Count > 0)
                Nodes[0].ExpandAll();
        }

        public void Update(SBViewport viewport)
        {
            if(viewport.Scene != null)
            LoadFromScene(viewport.Scene);
        }

        public void Step()
        {
            // none
        }

        public void Render(SBViewport viewport)
        {
            // none
        }

        public void AttachToPanel(SBViewportPanel viewportPanel)
        {
            Panel container = new Panel();
            container.AutoScroll = true;
            container.Dock = DockStyle.Fill;
            container.Controls.Add(BoneEditor);
            container.Controls.Add(new Splitter() { Dock = DockStyle.Top });
            container.Controls.Add(this);
            viewportPanel.TabPanel.AddTab("Bone", container);
        }
    }

    public class SBTreeNode : System.Windows.Forms.TreeNode
    {
        public SBTreeNode(string Name) : base(Name)
        {
            Checked = true;
        }
    }
}