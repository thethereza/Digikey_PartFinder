using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigikeyPartFinder.Properties;

namespace DigikeyPartFinder {
    public partial class License: Form {
        public License() {
            InitializeComponent();
        }


        private void button1_Click(object sender, EventArgs e) {
            Settings.Default.GPL = true;
            Settings.Default.Save();
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = checkBox1.Checked;
        }


    }
}
