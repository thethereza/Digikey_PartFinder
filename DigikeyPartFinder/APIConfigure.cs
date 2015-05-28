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
    public partial class APIConfigure: Form {
        public APIConfigure() {
            InitializeComponent();
            textBox_key.Text = Settings.Default.API_KEY;
        }

        private void button_ok_Click(object sender, EventArgs e) {
            Settings.Default.API_KEY = textBox_key.Text;
            Settings.Default.Save();
            this.Close();                
        }

        private void button_cancle_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(((LinkLabel)sender).Text);
        }
    }
}
