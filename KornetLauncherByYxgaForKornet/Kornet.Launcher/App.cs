using Kornet.Launcher.Windows;
using System;
using System.IO;
using System.Collections.Specialized;
using System.Reflection;
using System.Web;
using System.Windows.Forms;

namespace Kornet.Launcher;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? placeId = null;
        string? ticket = null;
        string year = "2016";

        if (args.Length != 0)
        {
            string uriString = args[0];
            int length = uriString.IndexOf('#');
            if (length >= 0)
                uriString = uriString.Substring(0, length);

            if (uriString.StartsWith("kornetclient://", StringComparison.OrdinalIgnoreCase))
            {
                NameValueCollection queryString = HttpUtility.ParseQueryString(new Uri(uriString).Query);
                placeId = queryString["place"] ?? queryString["placeId"];
                ticket = queryString["ticket"];
                year = queryString["year"] ?? year;
                if (queryString["2020"] == "true") year = "2020";
                if (queryString["2018"] == "true") year = "2018";
            }
        }

        Application.Run(new LauncherWindow(placeId, ticket, year));
    }
}