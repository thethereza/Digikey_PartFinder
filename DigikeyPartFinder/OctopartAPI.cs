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
using System.Dynamic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;



namespace DigikeyPartFinder {

    public class Result {
        public string name;
        public Dictionary<int, float> prices = new Dictionary<int, float>();
        public Dictionary<string, string> specs = new Dictionary<string, string>();
        public List<int> qtyList= new List<int>();
        public string sku;
        public string mpn;
        public int inventory;
        public float value;
        public float voltageRating;
        public string tolerance;
        public string package;
        public string packaging;
        public string dielectric;
        public string imageURL;
        public string seller;

        public string GetSpec(string key) {
            if (specs.ContainsKey(key)) return specs[key];
            return "";
        }

        //this will return the price if present, if not the price
        //for the next lower quantity, else 0 if not a valid quantity
        public float GetPrice(int qty) {
            int i;
            if (prices.ContainsKey(qty)) return prices[qty];
            for (i=0; i<qtyList.Count; i++) if (qtyList[i] >= qty) break;
            if (i == qtyList.Count) return 0f;
            if (i > 0) return prices[qtyList[i-1]];
            return 0f;
        }

        public void Finish() {
            //generate ordered list of quantities
            foreach (int x in prices.Keys.OrderBy(x=>x).ToArray()) qtyList.Add(x);
        }
    }


    public class OctopartAPI {
        private string url = "http://octopart.com/api/v3/parts/search";
        public enum ComponentType {
            Capacitor,
            Resistor
        };

        public static string ToScientific(string str) {
            Regex r = new Regex(@"([\.0-9]+)([mkunpMG])");
            Match m = r.Match(str);
            if (m.Success) {
                double val = double.Parse(m.Groups[1].Value);
                switch (m.Groups[2].Value.ToString()) {
                    case "m": val *= 1E-3; break;
                    case "k": val *= 1E3; break;
                    case "u": val *= 1E-6; break;
                    case "n": val *= 1E-9; break;
                    case "p": val *= 1E-12; break;
                    case "M": val *= 1E6; break;
                    case "G": val *= 1E9; break;
                }
                return val.ToString("E3");
            }
            r = new Regex(@"([\.0-9]+)$");
            m = r.Match(str);
            if (m.Success) {
                float val = float.Parse(m.Groups[1].Value.ToString());
                return val.ToString("E3");
            }
            throw new Exception("Unable to parse " + str);
        }

        public List<Result> Get(string size, string value, ComponentType type, string tolerance) {
            NameValueCollection nvc = new NameValueCollection();
            if (Settings.Default.API_KEY == null) {
                MessageBox.Show("Set API Key First (under file)");
                return null;
            }
            nvc.Add("apikey", Settings.Default.API_KEY);
            nvc.Add("start", "0");
            nvc.Add("limit", "100");
            nvc.Add("sortby", "avg_price asc");
            nvc.Add("filter[queries][]", "offers.seller.name:Digi-Key");
            nvc.Add("filter[queries][]", "specs.case_package.value:" + size);
            nvc.Add("include[]", "specs");
            nvc.Add("include[]", "imagesets");

            switch (type) {
                case ComponentType.Capacitor:
                    nvc.Add("q", "CAPACITOR");
                    nvc.Add("filter[queries][]", "specs.dielectric_characteristic.value:(X5R or X7R or C0G/NP0)");
                    nvc.Add("filter[queries][]", "specs.capacitance.value:" + ToScientific(value));
                    break;
                case ComponentType.Resistor:
                    nvc.Add("q", "RESISTOR");
                    nvc.Add("filter[queries][]", "specs.resistance.value:" + ToScientific(value));
                    // this doesn't seem to work.. not sure how to specify a tolerance
                    if (!tolerance.Equals("Any%")) 
                        nvc.Add("filter[queries][]", "specs.resistance_tolerance.value:±"+tolerance);
                    break;
            }


            string q = url + HtmlUtil.ToQueryString(nvc);
            string response;
            using (var wb = new WebClient()) {
                response = wb.DownloadString(q);
            }
            Clipboard.SetText(response);
            List<Result> resultList = new List<Result>();


            dynamic r = JsonConvert.DeserializeObject<dynamic>(response);
            foreach (var result in r.results) {
                var item = result.item;
                foreach (var offer in item.offers) {
                    // skip offers that are not from digikey and have less than qty 1000 in stock
                    if (((int)offer.in_stock_quantity < 1000)) continue;
                    Result rr = new Result();
                    rr.seller = (string)offer.seller.name;
                    rr.name = HtmlUtil.StripTagsRegex(result.snippet.ToString());
                    rr.mpn = (string)item.mpn;
                    rr.package = (string)item.specs.case_package.value[0];
                    rr.sku = (string)offer.sku;
                    rr.inventory = (int)offer.in_stock_quantity;
                    rr.packaging = (string)offer.packaging;
                    // see if we got a url for an image
                    try { rr.imageURL = (string)item.imagesets[0].small_image.url; } catch { }
                    // pull out some standard values
                    if (type == ComponentType.Capacitor) {
                        rr.value = (float)item.specs.capacitance.value[0];
                        rr.voltageRating = (float)item.specs.voltage_rating_dc.value[0];
                        try { rr.tolerance = (string)item.specs.capacitance_tolerance.value[0]; } catch { rr.tolerance = ""; }
                        try { rr.dielectric = (string)item.specs.dielectric_characteristic.value[0]; } catch { rr.dielectric = ""; }
                    }
                    if (type == ComponentType.Resistor) {
                        rr.value = (float)item.specs.resistance.value[0];
                        try { rr.tolerance = (string)item.specs.resistance_tolerance.value[0]; } catch { rr.tolerance = ""; }
                    }
                    // now pull out everything else
                    var list = ((JObject)item.specs).Properties().Select(p => p.Name).ToList();
                    foreach (var key in list) {
                        rr.specs.Add(key.ToString(), item.specs[key].value[0].ToString());
                    }
                    // pull pricing information
                    try {
                        foreach (var price in offer.prices.USD) {
                            rr.prices.Add((int)price[0], (float)price[1]);
                        }
                    } catch { }
       

                    rr.Finish();
                    if (rr.prices.Count > 0) resultList.Add(rr);

                }
            }
            return resultList;
        }

    }

    public static class HtmlUtil {
        public static string StripTagsRegex(string source) {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }
 
        public static string ToQueryString(NameValueCollection nvc) {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}",
                         HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value))).ToArray();
            return "?" + string.Join("&", array);
        }  
    }
}