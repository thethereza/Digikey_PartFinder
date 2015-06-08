   /*
    * ReThink Digikey Lookup
    Copyright (C) 2014  Reza Naima <reza@rethinkmedical.com>

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
    */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigikeyPartFinder.Properties;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Specialized;
using System.Web;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Text.RegularExpressions;


namespace DigikeyPartFinder {
    public partial class Form1: Form {
        OctopartAPI api;
        public bool quit = false;
        List<Result> res;
        Dictionary<string, Image> imageCache = new Dictionary<string, Image>();

        public Form1() {
            if (!Settings.Default.GPL) {
                License l = new License();
                l.ShowDialog();
                if (!Settings.Default.GPL) {
                    quit = true;
                    Application.Exit();
                    this.Close();
                }
            } 

            InitializeComponent();

            api = new OctopartAPI();
            comboBox_size.SelectedIndex = 0;
            comboBox_type.SelectedIndex = 0;
            comboBox1.SelectedIndex = 0;
            comboBox_tolerance.SelectedIndex = 0;
            imageCache.Add("none", new Bitmap(1, 1));
            grabPicturesToolStripMenuItem.Checked = Settings.Default.PICS;
            findCheapestSupplierToolStripMenuItem.Checked = Settings.Default.CHEAP;
        }


        private void button1_Click(object sender, EventArgs e) {
            string type = comboBox_type.SelectedItem.ToString();
            string size = comboBox_size.SelectedItem.ToString();
            string tolerance = comboBox_tolerance.SelectedItem.ToString();
            string value = textBox_value.Text;

            try {
                OctopartAPI.ToScientific(value);
            } catch {
                MessageBox.Show("Unable to parse value " + value);
                return;
            }

            if (Settings.Default.API_KEY == null || 
                Settings.Default.API_KEY.Length == 0) {
                MessageBox.Show("Set API Key First (under file)");
                return;
            }

            res = api.Get(size, value, (comboBox_type.SelectedIndex == 0) ? OctopartAPI.ComponentType.Capacitor : OctopartAPI.ComponentType.Resistor, tolerance);
            Refresh(sender, e);    
        }

        private Result FindCheaper(string mpn, int qty, float price) {
            Result cheapest = null;
            foreach (var x in res) {
                if (!x.seller.Equals("Digi-Key") && x.mpn.Equals(mpn)) {
                    if (x.GetPrice(qty) > 0 && x.GetPrice(qty) < price) {
                        cheapest = x;
                        price = x.GetPrice(qty);
                    }
                }
            }
            return cheapest;
        }

        private void Refresh(object sender, EventArgs e) {
            if (res == null) return;
            dataGridView.RowCount = 0; //clear

            //get list of all quantities
            Dictionary<int,string> aq = new Dictionary<int,string>();
            foreach (var x in res) {
                if (comboBox1.SelectedItem.ToString().Equals("Both") || comboBox1.SelectedItem.ToString().Equals(x.packaging)) {
                    foreach (var y in x.qtyList) if (!aq.ContainsKey(y)) aq.Add(y, "");
                }
            }
            List<int> laq = aq.Keys.OrderBy(x => x).ToList(); 
            //all list of quantities to datagridview
            //original list = 7 items
            dataGridView.ColumnCount = 7 + laq.Count;
            int i=0;
            foreach (var q in laq) {
                dataGridView.Columns[7 + i++].Name = "QTY" + q;
            }

            foreach (var r in res) {
                if (!r.seller.Equals("Digi-Key")) continue;
                if (comboBox1.SelectedItem.ToString().Equals("Both") || comboBox1.SelectedItem.ToString().Equals(r.packaging)) {
                    object[] row = new object[7 + laq.Count];
                    i = 0;

                    ///////////////////////
                    // ADD PICTURES IF SET
                    ///////////////////////
                    if (Settings.Default.PICS) {
                        // see if we have the image cahced, else grab it
                        if (r.imageURL != null && r.imageURL.Length > 1 && !imageCache.ContainsKey(r.imageURL)) {
                            try {
                                var request = WebRequest.Create(r.imageURL);
                                using (var response = request.GetResponse()) {
                                    using (var stream = response.GetResponseStream()) {
                                        Image img = Bitmap.FromStream(stream);
                                        float scale = 20f / (float)img.Height;
                                        imageCache.Add(r.imageURL, new Bitmap(img, new Size((int)((float)img.Width * scale), (int)((float)img.Height * scale))));
                                    }
                                }
                            } catch {
                                imageCache.Add(r.imageURL, imageCache["none"]);
                            }
                        }
                    }
                    //add picture if one is specified
                    if (r.imageURL != null && imageCache.ContainsKey(r.imageURL)) {
                        row[i++] = imageCache[r.imageURL];
                    } else {
                        row[i++] = imageCache["none"];
                    }


                    //POPULATE REST OF FIELDS
                    row[i++] = r.name; // +" (" + r.packaging + ")";
                    row[i++] = r.value +" " + r.tolerance;
                    if (r.specs.ContainsKey("voltage_rating_dc")) row[i - 1] += " (" + r.GetSpec("voltage_rating_dc") + "V)";
                    row[i++] = r.package + " " + r.dielectric;
                    row[i++] = r.sku;
                    row[i++] = r.mpn;
                    row[i++] = r.inventory;

                    //POPULATE PRICES
                    foreach (var q in laq) {
                        float thisPrice = r.GetPrice(q);
                        if (thisPrice > 0) {
                            Result cheaper = FindCheaper(r.mpn, q, thisPrice);
                            if (Settings.Default.CHEAP && cheaper != null) {
                                //found cheaper elsewhere
                                row[i++] = String.Format("{0} ({1} at {2})", thisPrice, cheaper.GetPrice(q), cheaper.seller);
                            } else {
                                //not cheaper
                                row[i++] = thisPrice.ToString();
                            }

                        } else {
                            //no valid price for this qty
                            row[i++] = "";
                        }
                    }
                    dataGridView.Rows.Add(row);
                }
            }

            //deault to sort by column qty 10
            if (dataGridView.Columns.Count >= 8)
                dataGridView.Sort(dataGridView.Columns[7], ListSortDirection.Ascending);

            //auto-resize
            for (i = 0; i < dataGridView.Columns.Count; i++) {
                dataGridView.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
        }

        private void setupKeysToolStripMenuItem_Click(object sender, EventArgs e) {
            APIConfigure apic = new APIConfigure();
            apic.Show();
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e) {
            try {
                object value = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                switch (e.ColumnIndex) {
                    case 1: //description
                        Result result = res.Where(x => x.name.Equals(value.ToString())).First();
                        string msg = "";
                        foreach (string key in result.specs.Keys) {
                            msg += key + ":" + result.specs[key] + "\r\n";
                        }
                        MessageBox.Show(msg);
                        break;
                    case 4:
                        System.Diagnostics.Process.Start("http://www.digikey.com/product-search/en?lang=en&site=us&KeyWords=" + value.ToString());
                        break;
                    case 5:
                        Clipboard.SetText(value.ToString());
                        break;
                }
            } catch {}
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e) {
            MessageBox.Show("v0.1\r\nUse At Your Own Risk","ReThink Digikey Lookup");
        }

        private void grabPicturesToolStripMenuItem_Click(object sender, EventArgs e) {
            Settings.Default.PICS = grabPicturesToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void findCheapestSupplierToolStripMenuItem_Click(object sender, EventArgs e) {
            Settings.Default.CHEAP = findCheapestSupplierToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void comboBox_type_SelectedIndexChanged(object sender, EventArgs e) {
            if (comboBox_type.SelectedIndex == 0) {
                this.comboBox_tolerance.Items.Clear();
                this.comboBox_tolerance.Items.AddRange(new object[] { "Any%", "10%", "20%" });
                this.comboBox_tolerance.SelectedIndex = 0;
            } else {
                this.comboBox_tolerance.Items.Clear();
                this.comboBox_tolerance.Items.AddRange(new object[] { "Any%", "0.1%", "0.5%", "1%", "5%" });
                this.comboBox_tolerance.SelectedIndex = 3;
            }
        
        }

        private void pictureBox1_Click(object sender, EventArgs e) {
            System.Diagnostics.Process.Start("http://rethinkmedical.com/");
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(@"https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=GCYVFA8DT68P8");
        }
        


    }

  

}
