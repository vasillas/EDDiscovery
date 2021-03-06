﻿/*
 * Copyright © 2017 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EliteDangerousCore.DB;
using EliteDangerousCore;

namespace EDDiscovery.UserControls
{
    public partial class UserControlRouteTracker :   UserControlCommonBase
    {
        private Font displayfont;
        private string DbSave { get { return "RouteTracker" + ((displaynumber > 0) ? displaynumber.ToString() : ""); } }
        private SavedRouteClass currentRoute;
        private  HistoryEntry currentHE;
        private string lastsystem;

        public override Color ColorTransparency { get { return Color.Green; } }
        public override void SetTransparency(bool on, Color curcol)
        {
            pictureBox.BackColor = this.BackColor = curcol;
            Display();
        }

        #region Init

        public UserControlRouteTracker()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            displayfont = discoveryform.theme.GetFont;

            autoCopyWPToolStripMenuItem.Checked = SQLiteDBClass.GetSettingBool(DbSave + "autoCopyWP", false);
            autoSetTargetToolStripMenuItem.Checked = SQLiteDBClass.GetSettingBool(DbSave + "autoSetTarget", false);
            string ids = SQLiteDBClass.GetSettingString(DbSave + "SelectedRoute", "-1");        // for some reason, it was saved as a string.. so keep for backwards compat
            int? id = ids.InvariantParseIntNull();
            if ( id != null )
                currentRoute = SavedRouteClass.GetAllSavedRoutes().Find(r => r.Id.Equals(id.Value));  // may be null

            Display();

            discoveryform.OnHistoryChange += Display;
            discoveryform.OnNewEntry += NewEntry;
        }

        public override void Closing()
        {
            SQLiteDBClass.PutSettingBool(DbSave + "autoCopyWP", autoCopyWPToolStripMenuItem.Checked);
            SQLiteDBClass.PutSettingBool(DbSave + "autoSetTarget", autoSetTargetToolStripMenuItem.Checked);
            discoveryform.OnHistoryChange -= Display;
            discoveryform.OnNewEntry -= NewEntry;
        }

        #endregion

        #region Implementation

        public override void InitialDisplay()
        {
            Display(discoveryform.history);
        }

        private void Display(HistoryList hl)            // when user clicks around..  HE may be null here
        {
            currentHE = hl.GetLastFSD;
            Display();
        }

        private void NewEntry(HistoryEntry l, HistoryList hl)
        {
            currentHE = hl.GetLastFSD;
            Display();
        }

        private void Display()
        {
            if (currentHE == null)
                return;

            Display(currentHE.System);

            //t.Interval = 200; t.Tick += (s,e)=> { Display(currentRoute.PosAlongRoute(percent)); percent += 0.5; }; t.Start();  // debug to make it play thru.. leave
        }

       //  double percent = 39; Timer t = new Timer();// play thru harness

        private void Display(ISystem cursys)
        {
            if (currentRoute == null)
            {
                DisplayText("Please set a route, by right clicking", "");
                return;
            }

            if (currentRoute.Systems.Count == 0)
            {
                DisplayText(currentRoute.Name, "Route contains no waypoints");
                return;
            }

            string topline = "", bottomline = "";

            if (!cursys.HasCoordinate)
            {
                topline = String.Format("Unknown location");
                bottomline = "";
            }
            else
            {
                SavedRouteClass.ClosestInfo closest = currentRoute.ClosestTo(cursys);

                if (closest == null)  // if null, no systems found.. uh oh
                {
                    topline = String.Format("No systems in route have known co-ords");
                    bottomline = "";
                }
                else if (closest.system == null)  // this is returned if past last system
                {
                    topline = String.Format("Past Last WP{0} {1}", currentRoute.Systems.Count(), currentRoute.LastSystem);
                    bottomline = "";
                }
                else
                {
                    double distleft = closest.waypointdistleft + closest.disttowaypoint;

                    string mesg = "";
                    var jumpRange = SQLiteDBClass.GetSettingDouble("TripPanel1" + "JumpRange", -1.0);       //TBD Not a good idea.
                    if (jumpRange > 0)
                    {
                        int jumps = (int)Math.Ceiling(distleft / jumpRange);
                        if (jumps > 0)
                            mesg = "@ " + jumps.ToString() + ((jumps == 1) ? " jump" : " jumps");
                    }

                    topline = String.Format("{0} {1} WPs, {2:N1}ly, left {3:N1}ly {4}", currentRoute.Name, currentRoute.Systems.Count, currentRoute.CumulativeDistance(), distleft, mesg);
                    bottomline = String.Format("{0:N2}ly to WP{1} {2} @ {3},{4},{5}", closest.disttowaypoint, closest.waypoint + 1, closest.system.Name,
                        closest.system.X.ToString("0.#"), closest.system.Y.ToString("0.#"), closest.system.Z.ToString("0.#"));

                    //System.Diagnostics.Debug.WriteLine("T:" + topline + Environment.NewLine + "B:" + bottomline);
                    string name = closest.system.Name;

                    if (lastsystem == null || name.CompareTo(lastsystem) != 0)
                    {
                        if (autoCopyWPToolStripMenuItem.Checked)
                            Clipboard.SetText(name);

                        if (autoSetTargetToolStripMenuItem.Checked)
                        {
                            string targetName;
                            double x, y, z;
                            TargetClass.GetTargetPosition(out targetName, out x, out y, out z);
                            if (name.CompareTo(targetName) != 0)
                                TargetHelpers.setTargetSystem(this, discoveryform, name, false);
                        }

                        lastsystem = name;
                    }
                }
            }

            DisplayText(topline, bottomline);
        }

        void DisplayText(String topline, String bottomLine)
        {
            pictureBox.ClearImageList();
            Color textcolour = IsTransparent ? discoveryform.theme.SPanelColor : discoveryform.theme.LabelColor;
            Color backcolour = IsTransparent ? Color.Transparent : this.BackColor;
            pictureBox.AddTextAutoSize(new Point(10, 5), new Size(1000, 35), topline == null ? "" : topline, displayfont, textcolour, backcolour, 1.0F);
            pictureBox.AddTextAutoSize(new Point(10, 35), new Size(1000, 35), bottomLine == null ? "" : bottomLine, displayfont, textcolour, backcolour, 1.0F);
            pictureBox.Render();
        }

        #endregion

        #region UI

        private void autoCopyWPToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Display();
        }

        private void setRouteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExtendedControls.ConfigurableForm f = new ExtendedControls.ConfigurableForm();

            var routes = SavedRouteClass.GetAllSavedRoutes();
            var routenames = (from x in routes select x.Name).ToList();
            f.Add(new ExtendedControls.ConfigurableForm.Entry("Route", "", new Point(10, 40), new Size(400, 24), "Select route", routenames, new Size(400, 400)));
            f.Add(new ExtendedControls.ConfigurableForm.Entry("Cancel", typeof(ExtendedControls.ButtonExt), "Cancel", new Point(410-100, 80), new Size(100, 24), "Press to Cancel"));
            f.Trigger += (dialogname, controlname, tag) =>
            {
                if (controlname != "Route")
                    f.DialogResult = DialogResult.Cancel;
                else
                    f.DialogResult = DialogResult.OK;
                f.Close();
            };
            if ( f.ShowDialog(this.FindForm(), this.FindForm().Icon, new Size(430, 120), new Point(-999, -999), "Enter route") == DialogResult.OK )
            {
                string routename = f.Get("Route");
                currentRoute = routes.Find(x => x.Name.Equals(routename));       // not going to be null, but consider the upset.

                // currentRoute.TestHarness(); // enable for debug

                if (currentRoute != null)
                    SQLiteDBClass.PutSettingString(DbSave + "SelectedRoute", currentRoute.Id.ToStringInvariant());        // write ID back

                Display();
            }
        }

        #endregion


    }
}
