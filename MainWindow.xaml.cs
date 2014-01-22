using System;
using System.Collections.Generic;
//using System.Windows.Shapes;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace AdobeCurveAndMapReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private String SourceFileName;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void FileDrop(object sender, DragEventArgs e)
        {
            String infoMsg = FileDrop_Sub(e);
            if (infoMsg != null)
            {
                TextBox.Text = infoMsg;
            }
        }

        private string FileDrop_Sub(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return "Not a file!";

            String[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 1)
                return "Too many files!";

            SourceFileName = files[0];

            if (!File.Exists(SourceFileName))
                return "Not a file!";

            String parsingResult = String.Empty;
            if (String.Equals(Path.GetExtension(SourceFileName), ".ACV", StringComparison.OrdinalIgnoreCase))
            {
                AdobeCurves acv = new AdobeCurves(SourceFileName);
                parsingResult = acv.ToString();
            }
            else if (String.Equals(Path.GetExtension(SourceFileName), ".AMP", StringComparison.OrdinalIgnoreCase))
            {
                AdobeArbitraryMap amp = new AdobeArbitraryMap(SourceFileName);
                parsingResult = amp.ToString();
            }
            else
            {
                parsingResult = "Unrecognized file extension";
            }

            parsingResult = Path.GetFileName(SourceFileName) + Environment.NewLine + parsingResult;

            return parsingResult;
        }





        // http://www.adobe.com/devnet-apps/photoshop/fileformatashtml/#50577411_pgfId-1056330
        private class AdobeCurves
        {
            public UInt16 Version     { get; private set; }
            public UInt16 CurvesCount { get; private set; }
            public List<Curve> Curves { get; private set; }
            private String errorMessage;

            public class Curve
            {
                public UInt16 PointsCount;
                public List<Point> Points;

                public class Point
                {
                    public UInt16 Y;
                    public UInt16 X;
                }
            }

            public AdobeCurves() { }

            public AdobeCurves(String fileName)
            {
                FileStream fs = null;
                try
                {
                    fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (IOException)
                {
                    if (fs != null)
                        fs.Close();
                    errorMessage = "File already in use!";
                }

                using (BinaryReader br = new BinaryReader(fs, new UTF8Encoding(), true))
                {
                    Version = br.ReadUInt16Reverse();
                    CurvesCount = br.ReadUInt16Reverse();
                    Curves = new List<Curve>();
                    for (int c = 0; c < CurvesCount; ++c )
                    {
                        Curve curve = new Curve();
                        curve.PointsCount = br.ReadUInt16Reverse();
                        curve.Points = new List<Curve.Point>();
                        for (int p = 0; p < curve.PointsCount; ++p)
                        {
                            Curve.Point point = new Curve.Point();
                            point.Y = br.ReadUInt16Reverse();
                            point.X = br.ReadUInt16Reverse();
                            curve.Points.Add(point);
                        }
                        Curves.Add(curve);
                    }
                    // TODO handle extra curves marker
                }

                fs.Close();
            }

            public override String ToString()
            {
                if(errorMessage!=null)
                    return errorMessage;

                String res = "Curves Count:" + CurvesCount + Environment.NewLine;
                res += "Control Points coordinates (X,Y):";
                for(int c = 0; c<CurvesCount; ++c)
                {
                    res += Environment.NewLine + "Curve " + c + ": ";
                    Curve curve = Curves.ElementAt(c);
                    for (int p = 0; p < curve.PointsCount; ++p )
                    {
                        Curve.Point point = curve.Points.ElementAt(p);
                        res += String.Format("({0:D3},{1:D3}) ", point.X, point.Y);
                    }
                }
                return res;
            }

            public bool FromString(String text)
            {
                // TODO
                return false;
            }

            public void ToBinary(String filename)
            {
                // TODO
            }
        }



        // http://www.adobe.com/devnet-apps/photoshop/fileformatashtml/#50577411_pgfId-1070460
        private class AdobeArbitraryMap
        {
            public int CurvesCount { get; private set; }
            public List<Curve> Curves { get; private set; }

            public class Curve
            {
                public List<byte> Values;
            }

            public AdobeArbitraryMap(){}

            public AdobeArbitraryMap(String fileName)
            {
                byte[] bytes = File.ReadAllBytes(fileName);
                CurvesCount = bytes.Length / 256;
                Curves = new List<Curve>();
                for (int c = 0; c < CurvesCount; ++c )
                {
                    Curve curve = new Curve();
                    curve.Values = new List<byte>();
                    for (int i = 0; i < 256; ++i )
                    {
                        curve.Values.Add(bytes[256 * c + i]);
                    }
                    Curves.Add(curve);
                }
            }

            public override String ToString()
            {
                String res = "Curves Count:" + CurvesCount + Environment.NewLine;
                res += "Values at: ";
                for (int p = 0; p < 256; ++p)
                    res += String.Format("{0:D3} ", p);

                for (int c = 0; c < CurvesCount; ++c)
                {
                    res += Environment.NewLine + String.Format("Curve {0:D2}:  ", c);
                    Curve curve = Curves.ElementAt(c);
                    for (int p = 0; p < 256; ++p)
                    {
                        byte value = curve.Values.ElementAt(p);
                        res += String.Format("{0:D3} ", value);
                    }
                }
                return res;
            }

            /// <summary>
            /// Try to initialize from string. Returns success or failure flag.
            /// </summary>
            public bool FromString(String text)
            {
                try
                {
                    string[] lines = text.Split(new String[] { Environment.NewLine }, StringSplitOptions.None);

                    if (lines.Length <= 3)
                        return false;

                    CurvesCount = lines.Length - 3;
                    Curves = new List<Curve>();
                    for (int c = 0; c < CurvesCount; ++c)
                    {
                        String valuesString = lines[c + 3];
                        int trailingCharsLength = "Curve xx:  ".Length;
                        valuesString = valuesString.Substring(trailingCharsLength, valuesString.Length - trailingCharsLength);
                        String[] valuesStrings = valuesString.Split(new String[]{" "}, StringSplitOptions.RemoveEmptyEntries);
                        byte[] values = Array.ConvertAll<string, byte>(valuesStrings, byte.Parse);
                        Curve curve = new Curve();
                        curve.Values = values.ToList();
                        Curves.Add(curve);
                    }
                    return true;

                }
                catch (System.Exception /*ex*/)
                {
                    return false;
                }
            }

            public void ToBinary(String filename)
            {
                List<byte> bytes = new List<byte>();
                foreach(Curve curve in Curves)
                {
                    foreach(byte value in curve.Values)
                    {
                        bytes.Add(value);
                    }
                }
                File.WriteAllBytes(filename, bytes.ToArray());
            }
        }


        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.All;
            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                AdobeArbitraryMap ampTry = new AdobeArbitraryMap();
                AdobeCurves acvTry = new AdobeCurves();

                if (ampTry.FromString(TextBox.Text))
                {
                    SaveFileDialog dialog = new SaveFileDialog();
                    dialog.Filter = "Map Files(*.AMP)|*.AMP";
                    if (SourceFileName != null)
                        dialog.InitialDirectory = Path.GetDirectoryName(SourceFileName);
                    if (dialog.ShowDialog() == true)
                        ampTry.ToBinary(dialog.FileName);
                }
                else if (acvTry.FromString(TextBox.Text))
                {
                    SaveFileDialog dialog = new SaveFileDialog();
                    dialog.Filter = "Curve Files(*.ACV)|*.ACV";
                    if (SourceFileName != null)
                        dialog.InitialDirectory = Path.GetDirectoryName(SourceFileName);
                    if (dialog.ShowDialog() == true)
                        acvTry.ToBinary(dialog.FileName);
                }
                else
                {
                    MessageBox.Show("Not a supported format");
                }

                e.Handled = true;
            }
        }


    }

    public static class Helpers
    {
        public static UInt16 ReadUInt16Reverse(this BinaryReader br)
        {
            byte[] data = br.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }
    }


}
