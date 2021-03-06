﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StudioSB.Scenes;
using StudioSB.GUI.Attachments;
using System.Diagnostics;
using OpenTK;
using StudioSB.Rendering.Bounding;

namespace StudioSB.GUI
{
    /// <summary>
    /// Panel control for viewport that contains
    /// scene information and attachment panels
    /// </summary>
    public class SBViewportPanel : Panel
    {
        public float CurrentFrame
        {
            get
            {
                return animationBar.Frame;
            }
        }
        public int FrameCount
        {
            set
            {
                animationBar.FrameCount = value;
            }
        }

        public bool EnableAnimationBar
        {
            set
            {
                animationBar.Visible = value;
                ClearAnimation.Enabled = value;
                if(!value)
                    animationBar.Clear();
                if (!value && LoadedScene != null && LoadedScene.Skeleton != null)
                {
                    foreach (var mat in LoadedScene.GetMaterials())
                        mat.ClearAnimations();
                    LoadedScene.Skeleton.Reset();
                }
            }
        }

        private Panel ViewportPanel;

        private SBPopoutPanel animationPopoutPanel;
        private SBAnimationBar animationBar;

        public SBViewport Viewport
        {
            get => _viewport;
            set
            {
                _viewport = value;
            }
        }
        private SBViewport _viewport;

        public SBScene LoadedScene { get
            {
                return Viewport.Scene;
            } }

        private Timer RenderTimer { get; set; }

        public SBTabPanel TabPanel { get; set; }

        //TODO: attachments
        private System.Drawing.Size RightBarSize = new System.Drawing.Size(500, 400);
        
        private SBPopoutPanel RightPane { get; set; }

        private Stopwatch stopWatch;
        public int timing = 0;


        private ToolStrip toolStrip;
        private ToolStripButton ResetPose;
        private ToolStripButton ClearAnimation;
        private ToolStripButton SaveRender;

        public SBViewportPanel()
        {
            ApplicationSettings.SkinControl(this);
            BackColor = ApplicationSettings.MiddleColor;

            toolStrip = new ToolStrip();
            ApplicationSettings.SkinControl(toolStrip);

            ResetPose = new ToolStripButton();
            ResetPose.Text = "Bind Pose";
            ResetPose.Click += (sender, args) =>
            {
                if (LoadedScene != null && LoadedScene.Skeleton != null)
                {
                    LoadedScene.Skeleton.Reset();
                }
            };

            ClearAnimation = new ToolStripButton();
            ClearAnimation.Text = "Clear Animation";
            ClearAnimation.Click += (sender, args) =>
            {
                EnableAnimationBar = false;
            };
            
            SaveRender = new ToolStripButton();
            SaveRender.Text = "Save Screenshot";
            SaveRender.Click += (sender, args) =>
            {
                _viewport.SaveRender("render_" + DateTime.Now.ToString("yyyyMMddHHmmssffff") + ".png");
            };

            toolStrip.Items.Add(ResetPose);
            toolStrip.Items.Add(ClearAnimation);
            toolStrip.Items.Add(SaveRender);

            //Application.Idle += new EventHandler(TriggerViewportRender);
            stopWatch = new Stopwatch();
            stopWatch.Start();
            RenderTimer = new Timer();
            RenderTimer.Interval = 1000 / 120;
            RenderTimer.Tick += new EventHandler(TriggerViewportRender);
            RenderTimer.Start();

            Viewport = new SBViewport();
            Viewport.Dock = DockStyle.Fill;

            RightPane = new SBPopoutPanel(PopoutSide.Right, "<", ">");
            RightPane.Dock = DockStyle.Right;
            RightPane.BackColor = ApplicationSettings.BackgroundColor;

            TabPanel = new SBTabPanel();
            TabPanel.Dock = DockStyle.Fill;

            RightPane.Contents.Add(TabPanel);
            
            animationBar = new SBAnimationBar();
            animationBar.Dock = DockStyle.Bottom;

            animationBar.FrameCount = 0;

            /*animationPopoutPanel = new SBPopoutPanel(PopoutSide.Bottom);
            animationPopoutPanel.Contents.Add(animationBar);
            animationPopoutPanel.CloseText = "v";
            animationPopoutPanel.OpenText = "^";
            animationPopoutPanel.Expand();
            animationPopoutPanel.Visible = false;
            animationPopoutPanel.Dock = DockStyle.Bottom;*/

            ViewportPanel = new Panel();
            ViewportPanel.Dock = DockStyle.Fill;
            ViewportPanel.Controls.Add(Viewport);
            ViewportPanel.Controls.Add(animationBar);

            Viewport.MouseDoubleClick += Pick;

            Clear();
            Setup();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Pick(object sender, MouseEventArgs args)
        {
            int mouse_x = args.X;
            int mouse_y = args.Y;

            float x = (2.0f * mouse_x) / Viewport.Width - 1.0f;
            float y = 1.0f - (2.0f * mouse_y) / Viewport.Height;
            Vector4 va = Vector4.Transform(new Vector4(x, y, -1.0f, 1.0f), Viewport.Camera.MvpMatrix.Inverted());
            Vector4 vb = Vector4.Transform(new Vector4(x, y, 1.0f, 1.0f), Viewport.Camera.MvpMatrix.Inverted());

            Vector3 p1 = va.Xyz;
            Vector3 p2 = p1 - (va - (va + vb)).Xyz * 100;

            Ray r = new Ray(p1, p2);

            foreach(var attachment in Viewport.Attachments)
            {
                attachment.Pick(r);
            }
        }
        

        /// <summary>
        /// Raises a render frame event for the viewport.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void TriggerViewportRender(object sender, EventArgs e)
        {
            timing += (int)stopWatch.ElapsedMilliseconds;
            stopWatch.Reset();
            stopWatch.Start();

            if (!Viewport.IsDisposed && Viewport.IsIdle && timing > 16)
            {
                timing = timing % 16;

                Viewport.Updated = true;

                animationBar.Process();

                Viewport.Frame = CurrentFrame;
                Viewport.RenderFrame();
            }
        }
        
        /// <summary>
        /// Clears all information in scene
        /// </summary>
        public void Clear()
        {
            foreach (var att in Viewport.Attachments)
                att.OnRemove(this);

            Viewport.Attachments.Clear();

            TabPanel.ClearTabs();

            Viewport.Scene = null;

            EnableAnimationBar = false;
        }

        /// <summary>
        /// Sets up controls
        /// </summary>
        public void Setup()
        {
            Controls.Clear();
            Controls.Add(ViewportPanel);
            Controls.Add(toolStrip);
            Controls.Add(RightPane);
        }

        /// <summary>
        /// Adds an attachment to this panel and viewport
        /// </summary>
        public void AddAttachment(IAttachment attachment)
        {
            if (!attachment.AllowMultiple())
            {
                var remove = Viewport.Attachments.Find(e => e.GetType() == attachment.GetType());
                if (remove != null && remove is Control c)
                {
                    TabPanel.RemoveTab(c);
                }
                if (remove != null)
                    Viewport.Attachments.Remove(remove);
            }

            if (LoadedScene != null)
                if(LoadedScene.HasBones && LoadedScene.Skeleton != null)
                    LoadedScene.Skeleton.Reset();

            attachment.OnAttach(this);
            Viewport.Attachments.Add(attachment);
        }

        /// <summary>
        /// Removes attachment from panel and viewport
        /// </summary>
        /// <param name="attachment"></param>
        public void RemoveAttachment(IAttachment attachment)
        {
            if (!Viewport.Attachments.Contains(attachment))
                return;
            attachment.OnRemove(this);
            Viewport.Attachments.Remove(attachment);
        }

        /// <summary>
        /// Gets an attachment if it is present in the scene
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetAttachment<T>()
        {
            foreach(var att in Viewport.Attachments)
                if(att is T o)
                {
                    return o;
                }
            return default(T);
        }

        /// <summary>
        /// Sets the scene and loads information
        /// </summary>
        /// <param name="scene"></param>
        public void SetScene(SBScene scene)
        {
            Clear();
            Viewport.Scene = scene;

            if (scene == null)
                return;

            // basic attachments
            foreach(var v in scene.AttachmentTypes)
            {
                var attachment = MainForm.AttachmentTypes.Find(e => e.GetType() == v);
                if(attachment != null)
                {
                    attachment.Update(Viewport);
                    AddAttachment(attachment);
                }
            }

            Setup();
        }
    }
}
