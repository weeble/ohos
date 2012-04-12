using System;
using System.Collections.Generic;
using System.Text;
using OpenHome.Os.Platform;
using Yahoo.Yui.Compressor;
using System.IO;

namespace WebCompressor
{
    class Program
    {
        static int Main(string[] args)
        {
            OptionParser parser = new OptionParser(args);
            var jsOutOption = new OptionParser.OptionString(null, "--jsout", null, "Compress Javascript input files to the specified output file.", "OUTFILE");
            var cssOutOption = new OptionParser.OptionString(null, "--cssout", null, "Compress CSS input files to the specified output file.", "OUTFILE");
            parser.AddOption(jsOutOption);
            parser.AddOption(cssOutOption);
            parser.Parse();
            //Arguments commandLine = new Arguments(args);

            if (parser.HelpSpecified())
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine("usage:");
                Console.WriteLine("\tWebCompressor --jsout:OUTFILE INFILE...");
                Console.WriteLine("\tWebCompressor --cssout:OUTFILE INFILE...");
                Console.WriteLine("options:");
                Console.WriteLine("\t-h, --help\t Show this help message and exit."); 
                //Console.WriteLine("\t-j:, --jsdir:\t The path of the javacscript directory.  All *.js files will be compressed recursively.");
                Console.WriteLine("\t-jo:, --jsout:\t Compress Javscript input files to specified output file.");
                //Console.WriteLine("\t-c:, --cssdir:\t The path of the css directory.  All *.css files will be compressed recursively.");
                Console.WriteLine("\t-co:, --cssout:\t Compress CSS input files to specified output file.");
                return 0;
            }
            else
            {
                bool jsMode = jsOutOption.Value != null;
                bool cssMode = cssOutOption.Value != null;
                if (jsMode && cssMode)
                {
                    Console.WriteLine("Cannot compress Javascript and CSS simultaneously.");
                    return 1;
                }
                if (!jsMode && !cssMode)
                {
                    Console.WriteLine("Please specify Javascript or CSS via the --jsout or --cssout options.");
                    return 1;
                }
                if (jsMode)
                {
                    List<string> jsIn = new List<string>(parser.PosArgs);
                    string jsOut = jsOutOption.Value;
                        
                    Console.Write("Compressing Javascript...");
                    CompressJs(jsIn, jsOut);
                }

                if (cssMode)
                {
                    List<string> cssIn = new List<string>(parser.PosArgs);
                    string cssOut = cssOutOption.Value;

                    Console.WriteLine("Compressing Css...");
                    CompressCss(cssIn, cssOut);
                }
                return 0;
            }
        }

        private static void CompressCss(IEnumerable<string> inputFiles, string cssOutput)
        {
            StringBuilder sbCss = new StringBuilder();
            int count = 0;
            foreach (string file in inputFiles)
            {
                count += 1;
                using (StreamReader sr = new StreamReader(file))
                {
                    sbCss.Append(sr.ReadToEnd());
                }
            }
            string compressCss = CssCompressor.Compress(sbCss.ToString(), 0, CssCompressionType.StockYuiCompressor);
            Console.WriteLine(String.Format("Compressed {0} Css Files", count));
          
            using (StreamWriter sw = new StreamWriter(cssOutput))
            {
                sw.Write(compressCss);
                Console.WriteLine(String.Format("Written compressed css file to {0}", cssOutput));
            }
        }

        private static void CompressJs(IEnumerable<string> inputFiles, string jsOutput)
        {
            StringBuilder sbJs = new StringBuilder();
            int count = 0;
            foreach (string file in inputFiles)
            {
                count += 1;
                using (StreamReader sr = new StreamReader(file))
                {
                    sbJs.Append(sr.ReadToEnd());
                }
            }
            Console.WriteLine("count:{0}, isnull:{1}", count, sbJs.ToString() == null);
            string compressJs =  JavaScriptCompressor.Compress(sbJs.ToString());
            Console.WriteLine(String.Format("Compressed {0} Js Files", count));
            using (StreamWriter sw = new StreamWriter(jsOutput))
            {
                sw.Write(compressJs);
                Console.WriteLine(String.Format("Written compressed js file to {0}", jsOutput));
            }
        }

    
    }
}
